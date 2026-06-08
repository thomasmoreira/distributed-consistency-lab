using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Subscribes a service's durable queue to the exchange and consumes events idempotently:
/// each delivery runs through <see cref="IInboxProcessor"/>, so a redelivery (RabbitMQ is
/// at-least-once) produces the effect exactly once (ADR-002). Messages are acked after the
/// effect commits; a failure nacks with requeue so nothing is lost.
/// </summary>
public sealed partial class RabbitMqConsumerHost(
    IServiceScopeFactory scopeFactory,
    ConsumerRegistry registry,
    IEventSerializer serializer,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<RabbitMqConsumerOptions> consumerOptions,
    ILogger<RabbitMqConsumerHost> logger) : BackgroundService
{
    private readonly RabbitMqOptions _rabbit = rabbitOptions.Value;
    private readonly RabbitMqConsumerOptions _consumer = consumerOptions.Value;

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Host,
            Port = _rabbit.Port,
            UserName = _rabbit.Username,
            Password = _rabbit.Password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _consumer.PrefetchCount, global: false, stoppingToken);
        await _channel.ExchangeDeclareAsync(_rabbit.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(_consumer.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        foreach (var registration in registry.Registrations)
        {
            await _channel.QueueBindAsync(_consumer.QueueName, _rabbit.Exchange, registration.EventTypeName, cancellationToken: stoppingToken);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) => OnReceivedAsync(ea, stoppingToken);
        await _channel.BasicConsumeAsync(_consumer.QueueName, autoAck: false, consumer, stoppingToken);

        LogStarted(logger, _consumer.QueueName, registry.Registrations.Count);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task OnReceivedAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var channel = _channel!;
        var typeName = ea.BasicProperties.Type ?? string.Empty;
        var registration = registry.FindByTypeName(typeName);

        if (registration is null || !Guid.TryParse(ea.BasicProperties.MessageId, out var messageId))
        {
            LogUndeliverable(logger, typeName, ea.BasicProperties.MessageId);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        try
        {
            var @event = serializer.Deserialize(Encoding.UTF8.GetString(ea.Body.Span), registration.EventType);

            await using var scope = scopeFactory.CreateAsyncScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
            var handler = scope.ServiceProvider.GetRequiredService(registration.HandlerType);

            var ran = await inbox.ProcessOnceAsync(messageId, c => registration.Invoke(handler, @event, c), ct);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
            LogProcessed(logger, typeName, messageId, ran);
        }
#pragma warning disable CA1031 // a handler failure must requeue, not crash the consumer
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogProcessingFailed(logger, ex, typeName, messageId);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer started on queue {Queue} with {Bindings} binding(s).")]
    private static partial void LogStarted(ILogger logger, string queue, int bindings);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consumed {Type} ({MessageId}); effect ran: {Ran}.")]
    private static partial void LogProcessed(ILogger logger, string type, Guid messageId, bool ran);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping undeliverable message: type={Type}, message-id={MessageId}.")]
    private static partial void LogUndeliverable(ILogger logger, string type, string? messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process {Type} ({MessageId}); requeuing.")]
    private static partial void LogProcessingFailed(ILogger logger, Exception ex, string type, Guid messageId);
}
