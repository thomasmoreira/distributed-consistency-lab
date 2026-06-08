namespace BuildingBlocks.Messaging;

public sealed class OutboxDispatcherOptions
{
    /// <summary>How often to poll the outbox for pending rows.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Max rows drained per poll.</summary>
    public int BatchSize { get; set; } = 50;
}
