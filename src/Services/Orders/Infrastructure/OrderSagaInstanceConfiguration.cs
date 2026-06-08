using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Services.Orders.Saga;

namespace Services.Orders.Infrastructure;

internal sealed class OrderSagaInstanceConfiguration : IEntityTypeConfiguration<OrderSagaInstance>
{
    public void Configure(EntityTypeBuilder<OrderSagaInstance> builder)
    {
        builder.ToTable("saga_state");

        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20);
    }
}
