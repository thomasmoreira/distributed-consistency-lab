using System.Text;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shouldly;
using Testcontainers.RabbitMq;

namespace Tests.Integration;

/// <summary>
/// Proves F2 against real infrastructure: the same message delivered twice triggers the
/// consumer effect exactly once. The inbox dedups the redelivery (ADR-002).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RabbitMqConsumerHostTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Exchange = "dcl.events";
    private const string Queue = "test.inventory";
    private const string RoutingKey = nameof(OrderPlaced);

    private string _conn = null!;
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4").Build();

    public async Task InitializeAsync()
    {
        _conn = await postgres.CreateDatabaseAsync();
        await _rabbit.StartAsync();

        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task Redelivered_message_triggers_the_effect_exactly_once()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = cts.Token;

        var counter = new InvocationCounter();
        await using var provider = BuildProvider(counter);

        var host = provider.GetServices<IHostedService>().OfType<RabbitMqConsumerHost>().Single();
        await host.StartAsync(ct);

        try
        {
            // Enqueue the SAME message-id twice (a redelivery). The test declares the queue so
            // the messages are routed regardless of when the host finishes binding.
            var placed = new OrderPlaced(Guid.CreateVersion7(), "SKU-1", 1, 10m);
            var (type, payload) = new JsonEventSerializer().Serialize(placed);
            await PublishTwiceAsync(placed.Id, type, payload, ct);

            await WaitUntilAsync(async () => counter.Count >= 1 && await InboxCountAsync(ct) == 1, ct);
            await Task.Delay(1500, ct); // give the duplicate time to be consumed and deduped

            counter.Count.ShouldBe(1);
            (await InboxCountAsync(ct)).ShouldBe(1);
        }
        finally
        {
            await host.StopAsync(ct);
        }
    }

    private ServiceProvider BuildProvider(InvocationCounter counter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(counter);
        services.AddDbContext<MessagingTestDbContext>(o => o.UseNpgsql(_conn));
        services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<MessagingTestDbContext>());
        services.AddOutboxInbox();
        services.AddIntegrationEventConsumer<OrderPlaced, CountingConsumer>();
        services.AddRabbitMqConsumer(Queue);
        services.Configure<RabbitMqOptions>(ConfigureRabbit);
        return services.BuildServiceProvider();
    }

    private void ConfigureRabbit(RabbitMqOptions o)
    {
        var uri = new Uri(_rabbit.GetConnectionString());
        var parts = uri.UserInfo.Split(':', 2);
        o.Host = uri.Host;
        o.Port = uri.Port;
        o.Username = Uri.UnescapeDataString(parts[0]);
        o.Password = Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty);
        o.Exchange = Exchange;
    }

    private async Task PublishTwiceAsync(Guid messageId, string type, string payload, CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: ct);

        var props = new BasicProperties { MessageId = messageId.ToString(), Type = type, ContentType = "application/json" };
        var body = Encoding.UTF8.GetBytes(payload);

        for (var i = 0; i < 2; i++)
        {
            await channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
        }
    }

    private async Task<int> InboxCountAsync(CancellationToken ct)
    {
        await using var db = NewDb();
        return await db.Inbox.CountAsync(ct);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("Condition not met within the allotted time.");
    }

    private MessagingTestDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MessagingTestDbContext>()
            .UseNpgsql(_conn)
            .Options;
        return new MessagingTestDbContext(options);
    }
}
