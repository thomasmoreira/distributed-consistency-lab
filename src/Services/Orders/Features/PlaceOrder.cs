using BuildingBlocks.Messaging;
using Contracts;
using Services.Orders.Domain;
using Services.Orders.Infrastructure;
using Services.Orders.Saga;

namespace Services.Orders.Features;

public sealed record PlaceOrderRequest(string Sku, int Quantity, decimal Amount);

public sealed record PlaceOrderResponse(Guid OrderId);

/// <summary>
/// Creates a Pending order and enqueues <see cref="OrderPlaced"/> into the outbox in the
/// SAME unit of work, then commits once. This is the first real use of the phase-1 outbox:
/// state change and integration event are atomic — no dual-write (ADR-001).
/// </summary>
public sealed class PlaceOrderHandler(OrdersDbContext db, IOutbox outbox, TimeProvider clock)
{
    public async Task<PlaceOrderResponse> HandleAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = Order.Place(request.Sku, request.Quantity, request.Amount, clock.GetUtcNow());

        db.Orders.Add(order);
        db.Sagas.Add(OrderSagaInstance.Start(order.Id));
        outbox.Add(new OrderPlaced(order.Id, order.Sku, order.Quantity, order.Amount));

        await db.SaveChangesAsync(ct);

        return new PlaceOrderResponse(order.Id);
    }
}
