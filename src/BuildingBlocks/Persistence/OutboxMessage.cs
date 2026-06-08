namespace BuildingBlocks.Persistence;

/// <summary>
/// A pending (or already published) integration event, stored in the same database
/// as the aggregate so it can be written in one transaction (ADR-001).
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Equals the integration event Id; propagated as the broker message-id.</summary>
    public Guid Id { get; init; }

    /// <summary>Event type name, e.g. "order.placed".</summary>
    public required string Type { get; init; }

    /// <summary>Serialized event payload (JSON).</summary>
    public required string Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Null while pending; set after a successful publisher confirm.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Publish attempts — drives backoff and poison-message detection.</summary>
    public int Attempts { get; set; }
}
