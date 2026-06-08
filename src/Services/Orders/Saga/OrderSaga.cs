namespace Services.Orders.Saga;

/// <summary>States of the orchestration checkout saga (ADR-003).</summary>
public enum OrderSagaState
{
    Started,
    StockReserved,
    PaymentCharged,
    Completed,
    Compensating,
    Cancelled,
}

/// <summary>Events that drive the saga forward (replies from Inventory/Payments).</summary>
public enum OrderSagaTrigger
{
    StockReservedOk,
    StockReservationFailed,
    PaymentChargedOk,
    PaymentDeclined,
    StockReleased,
}

/// <summary>
/// Pure transition function for the orchestration saga — no I/O, fully unit-testable.
/// The orchestrator (BackgroundService, added by dotnet-dev) drives side effects;
/// this type only decides the next state.
///
/// Happy path:  Started -> StockReserved -> PaymentCharged -> Completed
/// Compensation: StockReserved --PaymentDeclined--> Compensating --StockReleased--> Cancelled
/// </summary>
public static class OrderSaga
{
    public static OrderSagaState Next(OrderSagaState state, OrderSagaTrigger trigger) =>
        (state, trigger) switch
        {
            (OrderSagaState.Started, OrderSagaTrigger.StockReservedOk) => OrderSagaState.StockReserved,
            (OrderSagaState.Started, OrderSagaTrigger.StockReservationFailed) => OrderSagaState.Cancelled,
            (OrderSagaState.StockReserved, OrderSagaTrigger.PaymentChargedOk) => OrderSagaState.Completed,
            (OrderSagaState.StockReserved, OrderSagaTrigger.PaymentDeclined) => OrderSagaState.Compensating,
            (OrderSagaState.Compensating, OrderSagaTrigger.StockReleased) => OrderSagaState.Cancelled,
            _ => throw new InvalidOperationException(
                $"Invalid saga transition: {trigger} is not allowed from {state}."),
        };

    /// <summary>Terminal states never transition again.</summary>
    public static bool IsTerminal(OrderSagaState state) =>
        state is OrderSagaState.Completed or OrderSagaState.Cancelled;
}
