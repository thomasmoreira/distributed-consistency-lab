using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Domain;
using Services.Orders.Features;
using Services.Orders.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Tests.Integration;

/// <summary>
/// Proves phase 2 (spec §10.2): placing an order persists the Order AND writes the
/// OrderPlaced event to the outbox in a single transaction — no dual-write.
/// </summary>
public sealed class PlaceOrderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Placing_an_order_persists_it_and_writes_OrderPlaced_to_the_outbox_atomically()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        // Act: place an order through the real handler (EF outbox + single SaveChanges).
        Guid orderId;
        await using (var db = CreateDbContext())
        {
            var handler = new PlaceOrderHandler(db, new EfOutbox(db, new JsonEventSerializer()), TimeProvider.System);
            var response = await handler.HandleAsync(new PlaceOrderRequest("SKU-1", 2, 49.90m), ct);
            orderId = response.OrderId;
        }

        orderId.ShouldNotBe(Guid.Empty);

        // Assert against a fresh context: the order is Pending...
        await using (var db = CreateDbContext())
        {
            var order = await db.Orders.SingleAsync(ct);
            order.Id.ShouldBe(orderId);
            order.Status.ShouldBe(OrderStatus.Pending);
            order.Sku.ShouldBe("SKU-1");

            // ...and exactly one pending outbox row of type OrderPlaced referencing the order.
            var outbox = await db.Outbox.SingleAsync(ct);
            outbox.Type.ShouldBe(nameof(Contracts.OrderPlaced));
            outbox.ProcessedAt.ShouldBeNull();
            outbox.Payload.ShouldContain(orderId.ToString());
        }
    }

    private OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new OrdersDbContext(options);
    }
}
