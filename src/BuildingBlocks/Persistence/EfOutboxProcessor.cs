using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Drains pending outbox rows inside one transaction holding a <c>FOR UPDATE SKIP LOCKED</c>
/// lock — so multiple dispatcher instances can run concurrently without grabbing the same
/// rows. A row is only marked processed after the publish callback confirms; if the publish
/// throws, the transaction rolls back and the rows are retried on the next poll (ADR-001).
/// </summary>
public sealed class EfOutboxProcessor(MessagingDbContext db) : IOutboxProcessor
{
    public async Task<int> ProcessPendingAsync(
        int batchSize,
        Func<OutboxRecord, CancellationToken, Task> publishAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publishAsync);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var pending = await db.Outbox
            .FromSqlInterpolated(
                $"""
                 SELECT id, type, payload, occurred_at, processed_at, attempts
                 FROM outbox
                 WHERE processed_at IS NULL
                 ORDER BY occurred_at
                 FOR UPDATE SKIP LOCKED
                 LIMIT {batchSize}
                 """)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            await publishAsync(new OutboxRecord(message.Id, message.Type, message.Payload), ct);
            message.ProcessedAt = DateTimeOffset.UtcNow;
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return pending.Count;
    }
}
