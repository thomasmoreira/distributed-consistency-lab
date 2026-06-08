using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Services.Payments.Domain;

namespace Services.Payments.Infrastructure;

/// <summary>
/// The Payments service context. Derives from <see cref="MessagingDbContext"/> so the charge
/// record, the emitted event (outbox) and the inbox mark commit atomically (ADR-001/002).
/// Everything lives in the <c>payments</c> schema (ADR-005).
/// </summary>
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : MessagingDbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
