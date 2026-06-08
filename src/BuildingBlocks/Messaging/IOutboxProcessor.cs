namespace BuildingBlocks.Messaging;

/// <summary>
/// Drains a batch of pending outbox rows inside a single DB transaction that holds a
/// <c>FOR UPDATE SKIP LOCKED</c> lock on the selected rows. The transaction (and the EF
/// dependency) lives in the persistence layer; the publish side is injected as a callback
/// so Messaging stays free of any EF reference (avoids the circular dependency).
/// </summary>
public interface IOutboxProcessor
{
    /// <returns>The number of rows published and marked as processed.</returns>
    Task<int> ProcessPendingAsync(
        int batchSize,
        Func<OutboxRecord, CancellationToken, Task> publishAsync,
        CancellationToken ct);
}
