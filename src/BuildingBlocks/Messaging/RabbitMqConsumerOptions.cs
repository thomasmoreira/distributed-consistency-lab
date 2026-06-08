namespace BuildingBlocks.Messaging;

public sealed class RabbitMqConsumerOptions
{
    /// <summary>Durable queue this service consumes from (one per service).</summary>
    public string QueueName { get; set; } = "dcl.consumer";

    /// <summary>Max unacked messages in flight. 1 keeps processing strictly sequential.</summary>
    public ushort PrefetchCount { get; set; } = 1;
}
