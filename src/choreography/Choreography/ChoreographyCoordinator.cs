using BuildingBlocks.Messaging;
using Contracts;
using Services.Orders.Domain;
using Services.Orders.Infrastructure;

namespace Choreography;

/// <summary>
/// Choreography variant of the Orders coordination (ADR-006). There is NO central saga and
/// NO persisted process state: each reaction decides solely from the <see cref="Order"/>'s
/// own status, and the order's outcome emerges from independent reactions to events.
/// The <c>Status == Pending</c> guard makes every reaction idempotent.
/// <para>
/// Contrast with the orchestration variant (<c>Services.Orders.Saga.OrderSagaCoordinator</c>),
/// which loads a persisted <c>saga_state</c> and advances an explicit state machine. Note
/// that Inventory and Payments are IDENTICAL in both styles — only this coordination differs.
/// </para>
/// </summary>
public sealed class ChoreographyCoordinator(OrdersDbContext db, IOutbox outbox)
{
    public Task OnPaymentChargedAsync(Guid orderId, CancellationToken ct) =>
        ReactAsync(orderId, order =>
        {
            order.Confirm();
            outbox.Add(new OrderConfirmed(orderId));
        }, ct);

    public Task OnStockReservationFailedAsync(Guid orderId, CancellationToken ct) =>
        ReactAsync(orderId, order =>
        {
            order.Cancel();
            outbox.Add(new OrderCancelled(orderId, "stock-unavailable"));
        }, ct);

    public Task OnPaymentFailedAsync(Guid orderId, CancellationToken ct) =>
        ReactAsync(orderId, order =>
            // Ask Inventory to release the reserved stock; the order is cancelled once the
            // StockReleased reply comes back (no central state tracks "compensating").
            outbox.Add(new ReleaseStockRequested(orderId, order.Sku, order.Quantity)), ct);

    public Task OnStockReleasedAsync(Guid orderId, CancellationToken ct) =>
        ReactAsync(orderId, order =>
        {
            order.Cancel();
            outbox.Add(new OrderCancelled(orderId, "payment-failed"));
        }, ct);

    private async Task ReactAsync(Guid orderId, Action<Order> react, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([orderId], ct);
        if (order is { Status: OrderStatus.Pending })
        {
            react(order);
        }
    }
}
