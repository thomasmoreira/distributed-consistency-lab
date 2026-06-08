using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Inventory.Consumers;
using Services.Inventory.Domain;
using Services.Inventory.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Tests.Integration;

/// <summary>
/// Proves phase 3b: Inventory reserves stock on OrderPlaced and emits the saga's next event,
/// idempotently — reprocessing the same message reserves once (ADR-002).
/// </summary>
public sealed class InventoryReserveStockTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Reserves_stock_once_and_emits_StockReserved_even_when_redelivered()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        await SeedStockAsync("SKU-A", 5, ct);
        var placed = new OrderPlaced(Guid.CreateVersion7(), "SKU-A", 2, 49.90m);

        await ConsumeAsync(placed, ct);
        await ConsumeAsync(placed, ct); // redelivery (same event id)

        await using var db = NewDb();
        var stock = await db.StockItems.SingleAsync(s => s.Sku == "SKU-A", ct);
        stock.Available.ShouldBe(3); // reserved exactly once

        // Filter by Type server-side (text); match the order on the jsonb payload client-side
        // (Postgres has no LIKE operator for jsonb).
        var reserved = await db.Outbox.Where(o => o.Type == nameof(StockReserved)).ToListAsync(ct);
        reserved.Count(r => r.Payload.Contains(placed.OrderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Emits_StockReservationFailed_when_stock_is_insufficient()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        await SeedStockAsync("SKU-B", 1, ct);
        var placed = new OrderPlaced(Guid.CreateVersion7(), "SKU-B", 2, 10m);

        await ConsumeAsync(placed, ct);

        await using var db = NewDb();
        var stock = await db.StockItems.SingleAsync(s => s.Sku == "SKU-B", ct);
        stock.Available.ShouldBe(1); // untouched

        var failed = await db.Outbox.Where(o => o.Type == nameof(StockReservationFailed)).ToListAsync(ct);
        failed.Count(r => r.Payload.Contains(placed.OrderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Releases_reserved_stock_once_and_emits_StockReleased_even_when_redelivered()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        await SeedStockAsync("SKU-C", 5, ct);
        var placed = new OrderPlaced(Guid.CreateVersion7(), "SKU-C", 2, 30m);
        await ConsumeAsync(placed, ct); // reserve: 5 -> 3

        var release = new ReleaseStockRequested(placed.OrderId, "SKU-C", 2);
        await ReleaseAsync(release, ct);
        await ReleaseAsync(release, ct); // redelivery

        await using var db = NewDb();
        var stock = await db.StockItems.SingleAsync(s => s.Sku == "SKU-C", ct);
        stock.Available.ShouldBe(5); // released exactly once, back to the original quantity

        var released = await db.Outbox.Where(o => o.Type == nameof(StockReleased)).ToListAsync(ct);
        released.Count(r => r.Payload.Contains(release.OrderId.ToString(), StringComparison.Ordinal)).ShouldBe(1);
    }

    private async Task ConsumeAsync(OrderPlaced placed, CancellationToken ct)
    {
        await using var db = NewDb();
        var consumer = new OrderPlacedConsumer(db, new EfOutbox(db, new JsonEventSerializer()));
        await new EfInboxProcessor(db).ProcessOnceAsync(placed.Id, c => consumer.ConsumeAsync(placed, c), ct);
    }

    private async Task ReleaseAsync(ReleaseStockRequested release, CancellationToken ct)
    {
        await using var db = NewDb();
        var consumer = new ReleaseStockRequestedConsumer(db, new EfOutbox(db, new JsonEventSerializer()));
        await new EfInboxProcessor(db).ProcessOnceAsync(release.Id, c => consumer.ConsumeAsync(release, c), ct);
    }

    private async Task SeedStockAsync(string sku, int available, CancellationToken ct)
    {
        await using var db = NewDb();
        db.StockItems.Add(StockItem.Create(sku, available));
        await db.SaveChangesAsync(ct);
    }

    private InventoryDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new InventoryDbContext(options);
    }
}
