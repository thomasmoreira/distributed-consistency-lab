using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Services.Inventory.Domain;

namespace Services.Inventory.Infrastructure;

/// <summary>
/// The Inventory service context. Derives from <see cref="MessagingDbContext"/> so the
/// stock change, the emitted event (outbox) and the inbox dedup mark all commit in one
/// transaction (ADR-001/002). Everything lives in the <c>inventory</c> schema (ADR-005).
/// </summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : MessagingDbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfiguration(new StockItemConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
