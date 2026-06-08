using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Runs a consumer effect at most once per message-id. The fast-path check on
/// <c>inbox</c> short-circuits duplicates; the PK on <see cref="InboxMessage.MessageId"/> is
/// the real guarantee — a concurrent duplicate that slips past the check fails the insert,
/// rolls back, and is reprocessed-then-skipped on redelivery (ADR-002). The effect's writes
/// and the inbox mark share the transaction, so they commit atomically.
/// </summary>
public sealed class EfInboxProcessor(MessagingDbContext db) : IInboxProcessor
{
    public async Task<bool> ProcessOnceAsync(Guid messageId, Func<CancellationToken, Task> effect, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(effect);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (await db.Inbox.AsNoTracking().AnyAsync(x => x.MessageId == messageId, ct))
        {
            await tx.CommitAsync(ct);
            return false;
        }

        await effect(ct);

        db.Inbox.Add(new InboxMessage { MessageId = messageId, ProcessedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return true;
    }
}
