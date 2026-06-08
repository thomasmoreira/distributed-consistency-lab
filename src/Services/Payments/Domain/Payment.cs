namespace Services.Payments.Domain;

public enum PaymentStatus
{
    Charged,
    Failed,
}

/// <summary>Record of a charge attempt for an order (one per order).</summary>
public sealed class Payment
{
    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    private Payment()
    {
        // EF Core materialization.
    }

    private Payment(Guid orderId, decimal amount, PaymentStatus status)
    {
        OrderId = orderId;
        Amount = amount;
        Status = status;
    }

    public static Payment Charged(Guid orderId, decimal amount) => new(orderId, amount, PaymentStatus.Charged);

    public static Payment Failed(Guid orderId, decimal amount) => new(orderId, amount, PaymentStatus.Failed);
}
