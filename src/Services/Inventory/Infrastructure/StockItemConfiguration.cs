using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Services.Inventory.Domain;

namespace Services.Inventory.Infrastructure;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(x => x.Sku);
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(100);
        builder.Property(x => x.Available).HasColumnName("available");
    }
}
