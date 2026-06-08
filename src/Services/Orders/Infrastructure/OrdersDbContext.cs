using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Services.Orders.Domain;
using Services.Orders.Saga;

namespace Services.Orders.Infrastructure;

/// <summary>
/// The Orders service context. Derives from <see cref="MessagingDbContext"/> so the
/// <c>orders</c> table and the <c>outbox</c>/<c>inbox</c> tables share one transaction —
/// the order and its OrderPlaced event commit atomically (ADR-001). Everything lives in
/// the <c>orders</c> schema (ADR-005).
/// </summary>
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : MessagingDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderSagaInstance> Sagas => Set<OrderSagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderSagaInstanceConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
