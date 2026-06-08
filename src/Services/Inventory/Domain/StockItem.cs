namespace Services.Inventory.Domain;

/// <summary>
/// Stock for a SKU. <see cref="TryReserve"/> decrements availability; <see cref="Release"/>
/// is the compensation used when a downstream step fails (phase 5).
/// </summary>
public sealed class StockItem
{
    public string Sku { get; private set; } = null!;

    public int Available { get; private set; }

    private StockItem()
    {
        // EF Core materialization.
    }

    private StockItem(string sku, int available)
    {
        Sku = sku;
        Available = available;
    }

    public static StockItem Create(string sku, int available)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(available);

        return new StockItem(sku, available);
    }

    public bool TryReserve(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (Available < quantity)
        {
            return false;
        }

        Available -= quantity;
        return true;
    }

    public void Release(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        Available += quantity;
    }
}
