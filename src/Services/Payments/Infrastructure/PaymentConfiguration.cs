using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Services.Payments.Domain;

namespace Services.Payments.Infrastructure;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
    }
}
