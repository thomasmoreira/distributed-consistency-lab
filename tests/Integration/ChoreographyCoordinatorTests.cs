using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Choreography;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Domain;
using Services.Orders.Infrastructure;
using Shouldly;

namespace Tests.Integration;

/// <summary>
/// Proves the choreography variant (ADR-006) reaches the same outcomes as the orchestration
/// saga, but with no central state — decisions come from the order's own status, and the
/// status guard makes reactions idempotent (ADR-002).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ChoreographyCoordinatorTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string _conn = null!;

    public async Task InitializeAsync()
    {
        _conn = await postgres.CreateDatabaseAsync();

        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Payment_charged_confirms_the_order_idempotently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceAsync(ct);

        await ReactAsync((c, t) => c.OnPaymentChargedAsync(orderId, t), ct);
        await ReactAsync((c, t) => c.OnPaymentChargedAsync(orderId, t), ct); // duplicate

        await using var db = NewDb();
        (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Confirmed);
        await AssertEmittedOnceAsync(db, nameof(OrderConfirmed), orderId, ct);
    }

    [Fact]
    public async Task Stock_reservation_failure_cancels_the_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceAsync(ct);

        await ReactAsync((c, t) => c.OnStockReservationFailedAsync(orderId, t), ct);

        await using var db = NewDb();
        (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Cancelled);
        await AssertEmittedOnceAsync(db, nameof(OrderCancelled), orderId, ct);
    }

    [Fact]
    public async Task Payment_failure_requests_release_then_cancels_on_StockReleased()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceAsync(ct);

        await ReactAsync((c, t) => c.OnPaymentFailedAsync(orderId, t), ct);

        await using (var db = NewDb())
        {
            (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Pending); // not cancelled yet
            await AssertEmittedOnceAsync(db, nameof(ReleaseStockRequested), orderId, ct);
        }

        await ReactAsync((c, t) => c.OnStockReleasedAsync(orderId, t), ct);

        await using (var db = NewDb())
        {
            (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Cancelled);
            await AssertEmittedOnceAsync(db, nameof(OrderCancelled), orderId, ct);
        }
    }

    private async Task<Guid> PlaceAsync(CancellationToken ct)
    {
        await using var db = NewDb();
        var order = Order.Place("SKU-1", 1, 100m, DateTimeOffset.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    private async Task ReactAsync(Func<ChoreographyCoordinator, CancellationToken, Task> action, CancellationToken ct)
    {
        await using var db = NewDb();
        var coordinator = new ChoreographyCoordinator(db, new EfOutbox(db, new JsonEventSerializer()));
        await action(coordinator, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task AssertEmittedOnceAsync(OrdersDbContext db, string eventType, Guid orderId, CancellationToken ct)
    {
        var rows = await db.Outbox.Where(o => o.Type == eventType).ToListAsync(ct);
        rows.Count(r => r.Payload.Contains(orderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    private OrdersDbContext NewDb() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_conn).Options);
}
