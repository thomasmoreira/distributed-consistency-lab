using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Serializes an integration event into a (type, payload) pair for the outbox.
/// The <c>type</c> is a stable discriminator (the event's type name) used both as the
/// broker routing key and, on the consuming side, to resolve the concrete type.
/// </summary>
public interface IEventSerializer
{
    (string Type, string Payload) Serialize(IntegrationEvent message);
}
