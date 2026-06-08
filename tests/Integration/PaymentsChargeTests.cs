using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Payments.Consumers;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;
using Shouldly;

namespace Tests.Integration;

/// <summary>
/// Proves phase 4a: Payments charges on StockReserved by a deterministic rule (decline above
/// a threshold) and emits PaymentCharged/PaymentFailed, idempotently (ADR-002).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class PaymentsChargeTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string _conn = null!;
    private readonly IPaymentGateway _gateway = new FakePaymentGateway(Options.Create(new PaymentOptions { DeclineAboveAmount = 1000m }));

    public async Task InitializeAsync()
    {
        _conn = await postgres.CreateDatabaseAsync();

        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Charges_once_and_emits_PaymentCharged_even_when_redelivered()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var reserved = new StockReserved(Guid.CreateVersion7(), "SKU-1", 1, 500m); // under the limit

        await ConsumeAsync(reserved, ct);
        await ConsumeAsync(reserved, ct); // redelivery

        await using var db = NewDb();
        var payment = await db.Payments.SingleAsync(p => p.OrderId == reserved.OrderId, ct);
        payment.Status.ShouldBe(PaymentStatus.Charged);

        var charged = await db.Outbox.Where(o => o.Type == nameof(PaymentCharged)).ToListAsync(ct);
        charged.Count(r => r.Payload.Contains(reserved.OrderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Declines_and_emits_PaymentFailed_when_amount_exceeds_the_limit()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var reserved = new StockReserved(Guid.CreateVersion7(), "SKU-1", 1, 1500m); // over the limit

        await ConsumeAsync(reserved, ct);

        await using var db = NewDb();
        var payment = await db.Payments.SingleAsync(p => p.OrderId == reserved.OrderId, ct);
        payment.Status.ShouldBe(PaymentStatus.Failed);

        var failed = await db.Outbox.Where(o => o.Type == nameof(PaymentFailed)).ToListAsync(ct);
        failed.Count(r => r.Payload.Contains(reserved.OrderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    private async Task ConsumeAsync(StockReserved reserved, CancellationToken ct)
    {
        await using var db = NewDb();
        var consumer = new StockReservedConsumer(db, new EfOutbox(db, new JsonEventSerializer()), _gateway);
        await new EfInboxProcessor(db).ProcessOnceAsync(reserved.Id, c => consumer.ConsumeAsync(reserved, c), ct);
    }

    private PaymentsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_conn)
            .Options;
        return new PaymentsDbContext(options);
    }
}
