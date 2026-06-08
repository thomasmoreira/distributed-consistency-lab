using System.Text.Json;
using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Default <see cref="IEventSerializer"/> using System.Text.Json. The discriminator is
/// the runtime type name (e.g. "OrderPlaced"); the payload is the concrete event JSON.
/// </summary>
/// <remarks>
/// Known limitation (acceptable for this lab): the discriminator is <c>GetType().Name</c>,
/// so renaming an event type silently breaks routing keys, and generic/nested types would
/// produce odd names. Our events are flat, sealed records in <c>Contracts</c>, so this holds.
/// In production this would be a stable, explicit mapping (e.g. an <c>[EventType("order.placed.v1")]</c>
/// attribute or a type registry) decoupled from the CLR type name.
/// </remarks>
public sealed class JsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public (string Type, string Payload) Serialize(IntegrationEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var type = message.GetType().Name;
        var payload = JsonSerializer.Serialize(message, message.GetType(), Options);
        return (type, payload);
    }
}
