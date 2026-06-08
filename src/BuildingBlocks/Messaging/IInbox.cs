namespace BuildingBlocks.Messaging;

/// <summary>
/// Deduplication store keyed by message-id. Checking + marking happens in the SAME
/// transaction as the business effect, yielding exactly-once-effect against the
/// broker's at-least-once delivery (ADR-002).
/// </summary>
public interface IInbox
{
    Task<bool> AlreadyProcessedAsync(Guid messageId, CancellationToken ct);

    Task MarkAsync(Guid messageId, CancellationToken ct);
}
