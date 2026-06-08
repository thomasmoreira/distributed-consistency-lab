using Services.Orders.Saga;

namespace Tests.Unit;

public class OrderSagaTests
{
    [Fact]
    public void Happy_path_reaches_Completed()
    {
        var s = OrderSagaState.Started;
        s = OrderSaga.Next(s, OrderSagaTrigger.StockReservedOk);
        s = OrderSaga.Next(s, OrderSagaTrigger.PaymentChargedOk);

        Assert.Equal(OrderSagaState.Completed, s);
        Assert.True(OrderSaga.IsTerminal(s));
    }

    [Fact]
    public void Payment_declined_compensates_and_cancels()
    {
        var s = OrderSagaState.Started;
        s = OrderSaga.Next(s, OrderSagaTrigger.StockReservedOk);
        s = OrderSaga.Next(s, OrderSagaTrigger.PaymentDeclined);

        Assert.Equal(OrderSagaState.Compensating, s);

        s = OrderSaga.Next(s, OrderSagaTrigger.StockReleased);

        Assert.Equal(OrderSagaState.Cancelled, s);
        Assert.True(OrderSaga.IsTerminal(s));
    }

    [Fact]
    public void Stock_reservation_failure_cancels_immediately()
    {
        var s = OrderSaga.Next(OrderSagaState.Started, OrderSagaTrigger.StockReservationFailed);

        Assert.Equal(OrderSagaState.Cancelled, s);
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => OrderSaga.Next(OrderSagaState.Started, OrderSagaTrigger.PaymentChargedOk));
    }
}
