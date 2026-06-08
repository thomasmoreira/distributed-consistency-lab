namespace BuildingBlocks.Messaging;

/// <summary>
/// The serialized form of an outbox row handed to the publisher. Carries only what
/// the broker needs — no EF/persistence types leak into the Messaging abstraction.
/// </summary>
public sealed record OutboxRecord(Guid Id, string Type, string Payload);
