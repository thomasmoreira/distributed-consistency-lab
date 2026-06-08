using System.Text;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Tests.Integration;

/// <summary>
/// Proves the phase-1 outbox guarantee against real infrastructure (ADR-001):
/// a row written to the outbox is published exactly once and marked processed,
/// and a second drain is a no-op (processed rows are never re-published).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OutboxDispatcherTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Exchange = "dcl.events";

    private string _conn = null!;

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4").Build();

    public async Task InitializeAsync()
    {
        _conn = await postgres.CreateDatabaseAsync();
        await _rabbit.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task Pending_event_is_published_exactly_once_and_marked_processed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = cts.Token;
        var serializer = new JsonEventSerializer();

        // Arrange: a queue bound to the exchange BEFORE publishing, so nothing is dropped.
        await using var verifier = await BrokerVerifier.CreateAsync(_rabbit.GetConnectionString(), Exchange, "OrderPlaced", ct);

        // Arrange: one event sitting in the outbox (written via the real EfOutbox + UoW).
        var placed = new OrderPlaced(Guid.CreateVersion7(), "SKU-1", 2, 49.90m);
        await using (var db = CreateDbContext())
        {
            new EfOutbox(db, serializer).Add(placed);
            await db.SaveChangesAsync(ct);
        }

        await using var publisher = new RabbitMqEventPublisher(BuildRabbitOptions());

        // Act: drain the outbox once.
        int firstDrain;
        await using (var db = CreateDbContext())
        {
            firstDrain = await new EfOutboxProcessor(db).ProcessPendingAsync(50, publisher.PublishAsync, ct);
        }

        // Assert: one row dispatched and marked processed.
        firstDrain.ShouldBe(1);
        await using (var db = CreateDbContext())
        {
            var row = await db.Outbox.SingleAsync(ct);
            row.ProcessedAt.ShouldNotBeNull();
        }

        // Assert: the broker received exactly one message, carrying the event id as message-id.
        var messages = await verifier.DrainAsync(ct);
        messages.Count.ShouldBe(1);
        messages[0].MessageId.ShouldBe(placed.Id.ToString());

        // Act + Assert: a second drain publishes nothing (processed rows are never re-sent).
        await using (var db = CreateDbContext())
        {
            var secondDrain = await new EfOutboxProcessor(db).ProcessPendingAsync(50, publisher.PublishAsync, ct);
            secondDrain.ShouldBe(0);
        }

        (await verifier.DrainAsync(ct)).Count.ShouldBe(0);
    }

    private TestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_conn)
            .Options;
        return new TestDbContext(options);
    }

    private IOptions<RabbitMqOptions> BuildRabbitOptions()
    {
        var uri = new Uri(_rabbit.GetConnectionString());
        var parts = uri.UserInfo.Split(':', 2);
        return Options.Create(new RabbitMqOptions
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = Uri.UnescapeDataString(parts[0]),
            Password = Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty),
            Exchange = Exchange,
        });
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : MessagingDbContext(options);

    /// <summary>Declares a queue bound to the exchange and drains it via polling pulls.</summary>
    private sealed class BrokerVerifier : IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly string _queue;

        private BrokerVerifier(IConnection connection, IChannel channel, string queue)
        {
            _connection = connection;
            _channel = channel;
            _queue = queue;
        }

        public static async Task<BrokerVerifier> CreateAsync(string amqpUri, string exchange, string routingKey, CancellationToken ct)
        {
            var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };
            var connection = await factory.CreateConnectionAsync(ct);
            var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
            var queue = (await channel.QueueDeclareAsync(cancellationToken: ct)).QueueName;
            await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: ct);

            return new BrokerVerifier(connection, channel, queue);
        }

        public async Task<IReadOnlyList<ReceivedMessage>> DrainAsync(CancellationToken ct)
        {
            var received = new List<ReceivedMessage>();

            // Poll a few times: publisher confirms guarantee the broker has the message,
            // but routing to the bound queue can lag by a hair.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                BasicGetResult? result;
                while ((result = await _channel.BasicGetAsync(_queue, autoAck: true, ct)) is not null)
                {
                    received.Add(new ReceivedMessage(
                        result.BasicProperties.MessageId,
                        Encoding.UTF8.GetString(result.Body.Span)));
                }

                if (received.Count > 0)
                {
                    break;
                }

                await Task.Delay(150, ct);
            }

            return received;
        }

        public async ValueTask DisposeAsync()
        {
            await _channel.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record ReceivedMessage(string? MessageId, string Body);
}
