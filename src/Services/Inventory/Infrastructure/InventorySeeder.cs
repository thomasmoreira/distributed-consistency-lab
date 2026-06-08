using Microsoft.EntityFrameworkCore;
using Services.Inventory.Domain;

namespace Services.Inventory.Infrastructure;

/// <summary>Seeds demo stock on first run so a checkout has something to reserve.</summary>
internal static class InventorySeeder
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken ct = default)
    {
        if (await db.StockItems.AnyAsync(ct))
        {
            return;
        }

        db.StockItems.Add(StockItem.Create("SKU-1", 100));
        db.StockItems.Add(StockItem.Create("SKU-2", 5));
        await db.SaveChangesAsync(ct);
    }
}
