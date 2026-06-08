using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Payments.Consumers;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;

namespace Services.Payments;

public static class PaymentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full Payments composition. Used by both the host and the test harness;
    /// the caller configures <c>RabbitMqOptions</c> (and optionally <c>PaymentOptions</c>) separately.
    /// </summary>
    public static IServiceCollection AddPayments(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<MessagingDbContext>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddOutboxInbox();
        services.AddRabbitMqPublisher(_ => { });
        services.AddOutboxDispatcher();

        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

        services.AddIntegrationEventConsumer<StockReserved, StockReservedConsumer>();
        services.AddRabbitMqConsumer("payments");

        return services;
    }
}
