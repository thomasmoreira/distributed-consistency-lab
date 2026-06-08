using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Orders.Consumers;
using Services.Orders.Features;
using Services.Orders.Infrastructure;
using Services.Orders.Saga;

namespace Services.Orders;

public static class OrdersServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full Orders composition (DbContext, outbox/inbox, publisher, dispatcher,
    /// the orchestration saga and its consumers). Used by both the host and the test harness;
    /// the caller configures <c>RabbitMqOptions</c> separately.
    /// </summary>
    public static IServiceCollection AddOrders(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

        services.AddOutboxInbox();
        services.AddRabbitMqPublisher(_ => { });
        services.AddOutboxDispatcher();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PlaceOrderHandler>();

        services.AddScoped<OrderSagaCoordinator>();
        services.AddIntegrationEventConsumer<StockReserved, StockReservedSagaConsumer>();
        services.AddIntegrationEventConsumer<StockReservationFailed, StockReservationFailedSagaConsumer>();
        services.AddIntegrationEventConsumer<PaymentCharged, PaymentChargedSagaConsumer>();
        services.AddIntegrationEventConsumer<PaymentFailed, PaymentFailedSagaConsumer>();
        services.AddIntegrationEventConsumer<StockReleased, StockReleasedSagaConsumer>();
        services.AddRabbitMqConsumer("orders");

        return services;
    }
}
