using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF-backed outbox/inbox. The caller must separately register a concrete
    /// <see cref="MessagingDbContext"/> (e.g. <c>AddDbContext&lt;MessagingDbContext, OrdersDbContext&gt;()</c>).
    /// </summary>
    public static IServiceCollection AddOutboxInbox(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventSerializer, JsonEventSerializer>();
        services.TryAddScoped<IOutbox, EfOutbox>();
        services.TryAddScoped<IInbox, EfInbox>();
        services.TryAddScoped<IOutboxProcessor, EfOutboxProcessor>();
        return services;
    }
}
