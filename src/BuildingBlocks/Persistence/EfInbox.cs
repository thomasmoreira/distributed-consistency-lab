using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence;

/// <summary>
/// EF-backed <see cref="IInbox"/>. The mark rides the consumer's unit of work, so the
/// business effect and the dedup record commit atomically (ADR-002). A duplicate delivery
/// either short-circuits on <see cref="AlreadyProcessedAsync"/> or fails the PK insert on
/// commit — both yield exactly-once-effect.
/// </summary>
public sealed class EfInbox(MessagingDbContext db) : IInbox
{
    public Task<bool> AlreadyProcessedAsync(Guid messageId, CancellationToken ct) =>
        db.Inbox.AsNoTracking().AnyAsync(x => x.MessageId == messageId, ct);

    public Task MarkAsync(Guid messageId, CancellationToken ct)
    {
        db.Inbox.Add(new InboxMessage
        {
            MessageId = messageId,
            ProcessedAt = DateTimeOffset.UtcNow,
        });

        return Task.CompletedTask;
    }
}
