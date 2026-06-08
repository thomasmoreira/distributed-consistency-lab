namespace Services.Orders.Saga;

/// <summary>
/// Persisted state of one order's saga (spec §5.3). Created when the order is placed and
/// advanced by each reply event via the pure <see cref="OrderSaga"/> state machine.
/// </summary>
public sealed class OrderSagaInstance
{
    public Guid OrderId { get; private set; }

    public OrderSagaState State { get; private set; }

    private OrderSagaInstance()
    {
        // EF Core materialization.
    }

    private OrderSagaInstance(Guid orderId, OrderSagaState state)
    {
        OrderId = orderId;
        State = state;
    }

    public static OrderSagaInstance Start(Guid orderId) => new(orderId, OrderSagaState.Started);

    public OrderSagaState Advance(OrderSagaTrigger trigger)
    {
        State = OrderSaga.Next(State, trigger);
        return State;
    }
}
