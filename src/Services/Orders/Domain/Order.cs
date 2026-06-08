namespace Services.Orders.Domain;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled,
}

/// <summary>
/// Order aggregate. Created in <see cref="OrderStatus.Pending"/>; later moved to
/// Confirmed/Cancelled by the saga (phases 4-5). Business rules live here, not in handlers.
/// </summary>
public sealed class Order
{
    public Guid Id { get; private set; }

    public string Sku { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal Amount { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Order()
    {
        // EF Core materialization.
    }

    private Order(Guid id, string sku, int quantity, decimal amount, DateTimeOffset createdAt)
    {
        Id = id;
        Sku = sku;
        Quantity = quantity;
        Amount = amount;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
    }

    public static Order Place(string sku, int quantity, decimal amount, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        return new Order(Guid.CreateVersion7(), sku, quantity, amount, createdAt);
    }

    public void Confirm() => Status = OrderStatus.Confirmed;

    public void Cancel() => Status = OrderStatus.Cancelled;
}
