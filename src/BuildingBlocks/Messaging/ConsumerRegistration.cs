using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Maps a broker message type (the event's type name, used as routing key) to the concrete
/// event type and an invoker that casts the resolved handler and dispatches the event.
/// </summary>
public sealed record ConsumerRegistration(
    string EventTypeName,
    Type EventType,
    Type HandlerType,
    Func<object, IntegrationEvent, CancellationToken, Task> Invoke);

/// <summary>Holds the consumer registrations discovered at DI configuration time.</summary>
public sealed class ConsumerRegistry
{
    private readonly List<ConsumerRegistration> _registrations = [];

    public IReadOnlyList<ConsumerRegistration> Registrations => _registrations;

    public void Add(ConsumerRegistration registration) => _registrations.Add(registration);

    public ConsumerRegistration? FindByTypeName(string eventTypeName) =>
        _registrations.FirstOrDefault(r => r.EventTypeName == eventTypeName);
}
