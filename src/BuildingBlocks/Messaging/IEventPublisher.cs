namespace BuildingBlocks.Messaging;

/// <summary>
/// Publishes an already-serialized outbox record to the broker and only returns after a
/// publisher confirm. Called exclusively by the <see cref="OutboxDispatcher"/> — never
/// from a handler (that would reintroduce the dual-write).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(OutboxRecord record, CancellationToken ct);
}
