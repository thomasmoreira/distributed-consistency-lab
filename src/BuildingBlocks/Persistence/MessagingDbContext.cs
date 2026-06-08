using BuildingBlocks.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Base context that every service derives from. It owns the per-service <c>outbox</c> and
/// <c>inbox</c> tables so that state changes and their integration events (and the inbox
/// dedup mark) commit in one local transaction — no dual-write (ADR-001/002).
/// </summary>
public abstract class MessagingDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
