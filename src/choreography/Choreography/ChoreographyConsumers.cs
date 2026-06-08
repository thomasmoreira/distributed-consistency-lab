using BuildingBlocks.Messaging;
using Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Choreography;

// Thin consumers for the choreography variant. Note there is NO StockReserved consumer here:
// in choreography, Orders only reacts to outcomes (charged / failed / released), not to every
// intermediate step — there is no central process to advance.

public sealed class PaymentChargedChoreographyConsumer(ChoreographyCoordinator coordinator) : IIntegrationEventConsumer<PaymentCharged>
{
    public Task ConsumeAsync(PaymentCharged message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return coordinator.OnPaymentChargedAsync(message.OrderId, ct);
    }
}

public sealed class StockReservationFailedChoreographyConsumer(ChoreographyCoordinator coordinator) : IIntegrationEventConsumer<StockReservationFailed>
{
    public Task ConsumeAsync(StockReservationFailed message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return coordinator.OnStockReservationFailedAsync(message.OrderId, ct);
    }
}

public sealed class PaymentFailedChoreographyConsumer(ChoreographyCoordinator coordinator) : IIntegrationEventConsumer<PaymentFailed>
{
    public Task ConsumeAsync(PaymentFailed message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return coordinator.OnPaymentFailedAsync(message.OrderId, ct);
    }
}

public sealed class StockReleasedChoreographyConsumer(ChoreographyCoordinator coordinator) : IIntegrationEventConsumer<StockReleased>
{
    public Task ConsumeAsync(StockReleased message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return coordinator.OnStockReleasedAsync(message.OrderId, ct);
    }
}

public static class ChoreographyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the choreography coordination for Orders (no saga state). Wire this instead
    /// of the orchestration consumers to run the checkout in choreography style.
    /// </summary>
    public static IServiceCollection AddChoreographyOrders(this IServiceCollection services)
    {
        services.AddScoped<ChoreographyCoordinator>();
        services.AddIntegrationEventConsumer<PaymentCharged, PaymentChargedChoreographyConsumer>();
        services.AddIntegrationEventConsumer<StockReservationFailed, StockReservationFailedChoreographyConsumer>();
        services.AddIntegrationEventConsumer<PaymentFailed, PaymentFailedChoreographyConsumer>();
        services.AddIntegrationEventConsumer<StockReleased, StockReleasedChoreographyConsumer>();
        return services;
    }
}
