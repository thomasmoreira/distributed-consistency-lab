using Contracts;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Enqueues an integration event into the outbox table within the SAME unit of work
/// as the aggregate change. This is what eliminates the dual-write (ADR-001).
/// </summary>
public interface IOutbox
{
    void Add(IntegrationEvent message);
}
