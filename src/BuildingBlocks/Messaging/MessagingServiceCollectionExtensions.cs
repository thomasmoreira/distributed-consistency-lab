using System.Linq;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Registers the RabbitMQ-backed <see cref="IEventPublisher"/> (singleton).</summary>
    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        Action<RabbitMqOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }

    /// <summary>Runs the <see cref="OutboxDispatcher"/> as a hosted background service.</summary>
    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        Action<OutboxDispatcherOptions>? configure = null)
    {
        services.Configure(configure ?? (_ => { }));
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers an idempotent consumer for <typeparamref name="TEvent"/>. The handler is
    /// resolved per message and dispatched inside the inbox transaction.
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventConsumer<TEvent>
    {
        services.AddScoped<THandler>();

        GetOrCreateRegistry(services).Add(new ConsumerRegistration(
            typeof(TEvent).Name,
            typeof(TEvent),
            typeof(THandler),
            (handler, @event, ct) => ((IIntegrationEventConsumer<TEvent>)handler).ConsumeAsync((TEvent)@event, ct)));

        return services;
    }

    /// <summary>Runs the <see cref="RabbitMqConsumerHost"/> bound to the registered consumers.</summary>
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        string queueName,
        Action<RabbitMqConsumerOptions>? configure = null)
    {
        services.TryAddSingleton<IEventSerializer, JsonEventSerializer>();
        services.Configure<RabbitMqConsumerOptions>(o =>
        {
            o.QueueName = queueName;
            configure?.Invoke(o);
        });
        services.AddHostedService<RabbitMqConsumerHost>();
        return services;
    }

    private static ConsumerRegistry GetOrCreateRegistry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ConsumerRegistry))?
            .ImplementationInstance as ConsumerRegistry;

        if (existing is not null)
        {
            return existing;
        }

        var registry = new ConsumerRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
