using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Publishes an event to the broker and only returns successfully after a publisher
/// confirm. Used by the outbox dispatcher — never call it directly from a handler
/// (that would reintroduce the dual-write).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IntegrationEvent message, CancellationToken ct);
}
