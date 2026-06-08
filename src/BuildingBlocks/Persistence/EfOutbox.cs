using BuildingBlocks.Messaging;
using Contracts;

namespace BuildingBlocks.Persistence;

/// <summary>
/// EF-backed <see cref="IOutbox"/>. Adds the serialized event to the tracked context but
/// does NOT save — it rides the caller's unit of work so the row commits in the same
/// transaction as the aggregate change (ADR-001).
/// </summary>
public sealed class EfOutbox(MessagingDbContext db, IEventSerializer serializer) : IOutbox
{
    public void Add(IntegrationEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var (type, payload) = serializer.Serialize(message);

        db.Outbox.Add(new OutboxMessage
        {
            Id = message.Id,
            Type = type,
            Payload = payload,
            OccurredAt = message.OccurredAt,
        });
    }
}
