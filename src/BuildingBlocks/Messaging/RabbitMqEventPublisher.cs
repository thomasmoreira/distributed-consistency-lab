using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Publishes outbox records to a durable topic exchange with publisher confirms enabled:
/// <see cref="IChannel.BasicPublishAsync"/> only completes once the broker acks, so the
/// dispatcher will not mark a row processed for an unconfirmed publish (ADR-001).
/// The <see cref="OutboxRecord.Id"/> travels as the AMQP message-id, anchoring the
/// downstream inbox deduplication (ADR-002).
/// </summary>
public sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options) : IEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(OutboxRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrEmpty(record.Type);
        ArgumentException.ThrowIfNullOrEmpty(record.Payload);

        var channel = await EnsureChannelAsync(ct);

        var properties = new BasicProperties
        {
            MessageId = record.Id.ToString(),
            Type = record.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: record.Type,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(record.Payload),
            cancellationToken: ct);
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);

            await _channel.ExchangeDeclareAsync(
                exchange: _options.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Nested finally blocks guarantee every resource is released even if an earlier
        // DisposeAsync throws — no leak of the connection or the semaphore.
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }
            }
            finally
            {
                _gate.Dispose();
            }
        }
    }
}
