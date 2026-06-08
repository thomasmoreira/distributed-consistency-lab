using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Consumes a single integration event type. Invoked by the idempotent consumer
/// after the inbox check passes, inside the consumer's transaction.
/// </summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IntegrationEvent
{
    Task ConsumeAsync(TEvent message, CancellationToken ct);
}
