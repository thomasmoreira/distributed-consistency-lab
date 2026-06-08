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
}
