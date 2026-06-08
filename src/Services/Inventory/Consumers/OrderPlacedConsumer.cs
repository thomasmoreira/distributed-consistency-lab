using BuildingBlocks.Messaging;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Inventory.Infrastructure;

namespace Services.Inventory.Consumers;

/// <summary>
/// Reacts to <see cref="OrderPlaced"/>: reserves stock and emits the saga's next event.
/// The stock decrement and the emitted event ride the inbox transaction (the consumer host
/// wraps this in <c>IInboxProcessor</c>), so reserve + emit + dedup commit atomically.
/// </summary>
public sealed class OrderPlacedConsumer(InventoryDbContext db, IOutbox outbox) : IIntegrationEventConsumer<OrderPlaced>
{
    public async Task ConsumeAsync(OrderPlaced message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        var stock = await db.StockItems.FirstOrDefaultAsync(s => s.Sku == message.Sku, ct);

        if (stock is not null && stock.TryReserve(message.Quantity))
        {
            outbox.Add(new StockReserved(message.OrderId, message.Sku, message.Quantity));
        }
        else
        {
            var reason = stock is null ? "unknown-sku" : "insufficient-stock";
            outbox.Add(new StockReservationFailed(message.OrderId, message.Sku, reason));
        }
    }
}
