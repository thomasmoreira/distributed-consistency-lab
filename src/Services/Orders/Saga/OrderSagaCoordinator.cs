using BuildingBlocks.Messaging;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Domain;
using Services.Orders.Infrastructure;

namespace Services.Orders.Saga;

/// <summary>
/// Drives one order's saga in reaction to a reply event. Loads the persisted instance,
/// advances the state machine, and applies the side effects of the new state: confirm the
/// order, cancel it, or request stock compensation. Does not save — it rides the consumer's
/// inbox transaction (state change + emitted events commit atomically).
/// </summary>
public sealed class OrderSagaCoordinator(OrdersDbContext db, IOutbox outbox)
{
    public async Task HandleAsync(Guid orderId, OrderSagaTrigger trigger, CancellationToken ct)
    {
        var saga = await db.Sagas.FindAsync([orderId], ct)
            ?? throw new InvalidOperationException($"No saga instance for order {orderId}.");

        var next = saga.Advance(trigger);

        switch (next)
        {
            case OrderSagaState.Completed:
                (await LoadOrderAsync(orderId, ct)).Confirm();
                outbox.Add(new OrderConfirmed(orderId));
                break;

            case OrderSagaState.Cancelled:
                (await LoadOrderAsync(orderId, ct)).Cancel();
                outbox.Add(new OrderCancelled(orderId, ReasonFor(trigger)));
                break;

            case OrderSagaState.Compensating:
                var order = await LoadOrderAsync(orderId, ct);
                outbox.Add(new ReleaseStockRequested(orderId, order.Sku, order.Quantity));
                break;

            default:
                // Intermediate states (StockReserved, PaymentCharged): only persist progress.
                break;
        }
    }

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken ct) =>
        await db.Orders.FindAsync([orderId], ct)
        ?? throw new InvalidOperationException($"Order {orderId} not found.");

    private static string ReasonFor(OrderSagaTrigger trigger) => trigger switch
    {
        OrderSagaTrigger.StockReservationFailed => "stock-unavailable",
        OrderSagaTrigger.StockReleased => "payment-failed",
        _ => "cancelled",
    };
}
