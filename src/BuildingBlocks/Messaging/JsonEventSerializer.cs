using System.Text.Json;
using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Default <see cref="IEventSerializer"/> using System.Text.Json. The discriminator is
/// the runtime type name (e.g. "OrderPlaced"); the payload is the concrete event JSON.
/// </summary>
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
