namespace BuildingBlocks.Messaging;

/// <summary>
/// Runs a consumer effect exactly once per message-id, inside one DB transaction that also
/// records the inbox mark — so the business effect and the dedup record commit atomically
/// (ADR-002). Like <see cref="IOutboxProcessor"/>, the transaction (and the EF dependency)
/// lives in the persistence layer; the effect is injected as a callback.
/// </summary>
public interface IInboxProcessor
{
    /// <returns><c>true</c> if the effect ran now; <c>false</c> if the message was already processed.</returns>
    Task<bool> ProcessOnceAsync(Guid messageId, Func<CancellationToken, Task> effect, CancellationToken ct);
}
