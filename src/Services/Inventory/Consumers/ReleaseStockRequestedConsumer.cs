using BuildingBlocks.Messaging;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Inventory.Infrastructure;

namespace Services.Inventory.Consumers;

/// <summary>
/// Compensation: on <see cref="ReleaseStockRequested"/> (issued by the saga when payment
/// fails), returns the previously reserved stock and replies <see cref="StockReleased"/>,
/// which lets the saga finalize the cancellation. Release + emit ride the inbox transaction,
/// so a redelivery releases exactly once (ADR-002).
/// </summary>
public sealed class ReleaseStockRequestedConsumer(InventoryDbContext db, IOutbox outbox)
    : IIntegrationEventConsumer<ReleaseStockRequested>
{
    public async Task ConsumeAsync(ReleaseStockRequested message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        var stock = await db.StockItems.FirstOrDefaultAsync(s => s.Sku == message.Sku, ct);
        stock?.Release(message.Quantity);

        // Always reply so the saga can finalize, even if the SKU row is unexpectedly missing.
        outbox.Add(new StockReleased(message.OrderId, message.Sku, message.Quantity));
    }
}
