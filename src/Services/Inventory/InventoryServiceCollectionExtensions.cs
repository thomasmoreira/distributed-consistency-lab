using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Inventory.Consumers;
using Services.Inventory.Infrastructure;

namespace Services.Inventory;

public static class InventoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full Inventory composition. Used by both the host and the test harness;
    /// the caller configures <c>RabbitMqOptions</c> separately.
    /// </summary>
    public static IServiceCollection AddInventory(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InventoryDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

        services.AddOutboxInbox();
        services.AddRabbitMqPublisher(_ => { });
        services.AddOutboxDispatcher();

        services.AddIntegrationEventConsumer<OrderPlaced, OrderPlacedConsumer>();
        services.AddIntegrationEventConsumer<ReleaseStockRequested, ReleaseStockRequestedConsumer>();
        services.AddRabbitMqConsumer("inventory");

        return services;
    }
}
