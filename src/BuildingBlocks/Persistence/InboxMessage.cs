namespace BuildingBlocks.Persistence;

/// <summary>
/// Marks a message-id as already processed by this service. The primary key on
/// <see cref="MessageId"/> is the deduplication lock (ADR-002).
/// </summary>
public sealed class InboxMessage
{
    public required Guid MessageId { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}
