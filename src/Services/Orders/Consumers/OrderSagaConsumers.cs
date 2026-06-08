using BuildingBlocks.Messaging;
using Contracts;
using Services.Orders.Saga;

namespace Services.Orders.Consumers;

// Thin consumers that translate each reply event into a saga trigger. The coordinator does
// the work; the consumer host wraps each in the inbox transaction (idempotent).

public sealed class StockReservedSagaConsumer(OrderSagaCoordinator saga) : IIntegrationEventConsumer<StockReserved>
{
    public Task ConsumeAsync(StockReserved message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return saga.HandleAsync(message.OrderId, OrderSagaTrigger.StockReservedOk, ct);
    }
}

public sealed class StockReservationFailedSagaConsumer(OrderSagaCoordinator saga) : IIntegrationEventConsumer<StockReservationFailed>
{
    public Task ConsumeAsync(StockReservationFailed message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return saga.HandleAsync(message.OrderId, OrderSagaTrigger.StockReservationFailed, ct);
    }
}

public sealed class PaymentChargedSagaConsumer(OrderSagaCoordinator saga) : IIntegrationEventConsumer<PaymentCharged>
{
    public Task ConsumeAsync(PaymentCharged message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return saga.HandleAsync(message.OrderId, OrderSagaTrigger.PaymentChargedOk, ct);
    }
}

public sealed class PaymentFailedSagaConsumer(OrderSagaCoordinator saga) : IIntegrationEventConsumer<PaymentFailed>
{
    public Task ConsumeAsync(PaymentFailed message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return saga.HandleAsync(message.OrderId, OrderSagaTrigger.PaymentDeclined, ct);
    }
}

public sealed class StockReleasedSagaConsumer(OrderSagaCoordinator saga) : IIntegrationEventConsumer<StockReleased>
{
    public Task ConsumeAsync(StockReleased message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return saga.HandleAsync(message.OrderId, OrderSagaTrigger.StockReleased, ct);
    }
}
