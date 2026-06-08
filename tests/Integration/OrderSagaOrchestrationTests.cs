using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Domain;
using Services.Orders.Features;
using Services.Orders.Infrastructure;
using Services.Orders.Saga;
using Shouldly;

namespace Tests.Integration;

/// <summary>
/// Proves phase 4b: the orchestration saga in Orders consumes the reply events, drives the
/// persisted state machine, and moves the order to its final outcome (spec §5.3, ADR-003).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OrderSagaOrchestrationTests(PostgresFixture postgres) : IAsyncLifetime
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
    public async Task Happy_path_confirms_the_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceOrderAsync(ct);

        await DriveAsync(orderId, OrderSagaTrigger.StockReservedOk, ct);
        await DriveAsync(orderId, OrderSagaTrigger.PaymentChargedOk, ct);

        await using var db = NewDb();
        (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Confirmed);
        (await db.Sagas.SingleAsync(s => s.OrderId == orderId, ct)).State.ShouldBe(OrderSagaState.Completed);
        await AssertEmittedAsync(orderId, nameof(OrderConfirmed), ct);
    }

    [Fact]
    public async Task Stock_reservation_failure_cancels_the_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceOrderAsync(ct);

        await DriveAsync(orderId, OrderSagaTrigger.StockReservationFailed, ct);

        await using var db = NewDb();
        (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Cancelled);
        (await db.Sagas.SingleAsync(s => s.OrderId == orderId, ct)).State.ShouldBe(OrderSagaState.Cancelled);
        await AssertEmittedAsync(orderId, nameof(OrderCancelled), ct);
    }

    [Fact]
    public async Task Payment_failure_compensates_then_cancels_the_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var orderId = await PlaceOrderAsync(ct);

        await DriveAsync(orderId, OrderSagaTrigger.StockReservedOk, ct);
        await DriveAsync(orderId, OrderSagaTrigger.PaymentDeclined, ct);

        // Saga is compensating and has asked Inventory to release the stock.
        await using (var db = NewDb())
        {
            (await db.Sagas.SingleAsync(s => s.OrderId == orderId, ct)).State.ShouldBe(OrderSagaState.Compensating);
        }

        await AssertEmittedAsync(orderId, nameof(ReleaseStockRequested), ct);

        // Inventory replies StockReleased -> saga finalizes as cancelled.
        await DriveAsync(orderId, OrderSagaTrigger.StockReleased, ct);

        await using (var db = NewDb())
        {
            (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Cancelled);
            (await db.Sagas.SingleAsync(s => s.OrderId == orderId, ct)).State.ShouldBe(OrderSagaState.Cancelled);
        }

        await AssertEmittedAsync(orderId, nameof(OrderCancelled), ct);
    }

    private async Task<Guid> PlaceOrderAsync(CancellationToken ct)
    {
        await using var db = NewDb();
        var handler = new PlaceOrderHandler(db, new EfOutbox(db, new JsonEventSerializer()), TimeProvider.System);
        var response = await handler.HandleAsync(new PlaceOrderRequest("SKU-1", 1, 100m), ct);
        return response.OrderId;
    }

    private async Task DriveAsync(Guid orderId, OrderSagaTrigger trigger, CancellationToken ct)
    {
        await using var db = NewDb();
        await new OrderSagaCoordinator(db, new EfOutbox(db, new JsonEventSerializer())).HandleAsync(orderId, trigger, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task AssertEmittedAsync(Guid orderId, string eventType, CancellationToken ct)
    {
        await using var db = NewDb();
        var rows = await db.Outbox.Where(o => o.Type == eventType).ToListAsync(ct);
        rows.Count(r => r.Payload.Contains(orderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    private OrdersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_conn)
            .Options;
        return new OrdersDbContext(options);
    }
}
