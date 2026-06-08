namespace Contracts;

/// <summary>
/// Base type for every cross-service integration event.
/// <para>
/// <see cref="Id"/> doubles as the message-id propagated in the broker header and
/// as the outbox/inbox primary key — it is the anchor for exactly-once-effect
/// (see ADR-001 / ADR-002).
/// </para>
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
