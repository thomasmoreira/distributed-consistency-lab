namespace BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>Durable topic exchange events are published to. Routing key = event type.</summary>
    public string Exchange { get; set; } = "dcl.events";
}
