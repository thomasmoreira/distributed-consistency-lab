using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Inventory;
using Services.Inventory.Domain;
using Services.Inventory.Infrastructure;
using Services.Orders;
using Services.Orders.Domain;
using Services.Orders.Features;
using Services.Orders.Infrastructure;
using Services.Orders.Saga;
using Services.Payments;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Tests.Integration;

/// <summary>
/// The flagship suite (spec §7). Two guarantees against real Postgres + RabbitMQ:
/// <list type="bullet">
/// <item>F1 — when the broker is unavailable, the event is not lost: it waits in the outbox
/// and publishes on recovery.</item>
/// <item>Exactly-once-effect — the full three-service checkout completes once: charged once,
/// stock reserved once.</item>
/// </list>
/// </summary>
public sealed class EndToEndResilienceTests : IAsyncLifetime
{
    private const int InitialStock = 100;
    private const int OrderQuantity = 2;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4").Build();
    private readonly List<IHost> _hosts = [];

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            try
            {
                await host.StopAsync();
            }
#pragma warning disable CA1031
            catch (Exception)
#pragma warning restore CA1031
            {
                // best-effort teardown
            }

            host.Dispose();
        }

        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task Outbox_holds_the_event_while_the_broker_is_down_and_publishes_on_recovery()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = cts.Token;

        var orders = BuildHost(s => s.AddOrders(Conn("orders")));
        _hosts.Add(orders);
        await MigrateAsync(orders.Services.GetRequiredService<OrdersDbContext>(), ct);
        await orders.StartAsync(ct);

        // Freeze the broker, then place the order: it commits to the outbox but the dispatcher
        // cannot publish it.
        await DockerAsync("pause", ct);
        var orderId = await PlaceOrderAsync(orders, ct);

        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        await using (var db = NewOrders())
        {
            var row = await db.Outbox.SingleAsync(ct);
            row.ProcessedAt.ShouldBeNull(); // nothing published while the broker is down
        }

        // Broker is back: the dispatcher publishes the row it was holding (nothing lost).
        await DockerAsync("unpause", ct);
        await WaitUntilAsync(
            async () =>
            {
                await using var db = NewOrders();
                var row = await db.Outbox.FirstOrDefaultAsync(ct);
                return row?.ProcessedAt is not null;
            },
            TimeSpan.FromSeconds(30),
            () => Task.FromResult($"OrderPlaced for {orderId} was never published"),
            ct);
    }

    [Fact]
    public async Task Checkout_completes_exactly_once_across_the_three_services()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = cts.Token;

        var orders = BuildHost(s => s.AddOrders(Conn("orders")));
        var inventory = BuildHost(s => s.AddInventory(Conn("inventory")));
        var payments = BuildHost(s => s.AddPayments(Conn("payments")));
        _hosts.AddRange([orders, inventory, payments]);

        await MigrateAsync(orders.Services.GetRequiredService<OrdersDbContext>(), ct);
        await MigrateAsync(inventory.Services.GetRequiredService<InventoryDbContext>(), ct);
        await MigrateAsync(payments.Services.GetRequiredService<PaymentsDbContext>(), ct);
        await SeedStockAsync(ct);

        await orders.StartAsync(ct);
        await inventory.StartAsync(ct);
        await payments.StartAsync(ct);

        var orderId = await PlaceOrderAsync(orders, ct);

        await WaitUntilAsync(async () =>
        {
            await using var db = NewOrders();
            return (await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct))?.Status == OrderStatus.Confirmed;
        }, TimeSpan.FromMinutes(2), () => DumpAsync(ct), ct);

        await using (var db = NewOrders())
        {
            (await db.Orders.SingleAsync(o => o.Id == orderId, ct)).Status.ShouldBe(OrderStatus.Confirmed);
            (await db.Sagas.SingleAsync(s => s.OrderId == orderId, ct)).State.ShouldBe(OrderSagaState.Completed);
        }

        await using (var db = NewPayments())
        {
            var charges = await db.Payments.Where(p => p.OrderId == orderId).ToListAsync(ct);
            charges.Count.ShouldBe(1); // charged exactly once
            charges[0].Status.ShouldBe(PaymentStatus.Charged);
        }

        await using (var db = NewInventory())
        {
            (await db.StockItems.SingleAsync(s => s.Sku == "SKU-1", ct)).Available.ShouldBe(InitialStock - OrderQuantity);
        }
    }

    private static async Task<Guid> PlaceOrderAsync(IHost orders, CancellationToken ct)
    {
        await using var scope = orders.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlaceOrderHandler>();
        return (await handler.HandleAsync(new PlaceOrderRequest("SKU-1", OrderQuantity, 100m), ct)).OrderId;
    }

    private IHost BuildHost(Action<IServiceCollection> register)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        register(builder.Services);
        builder.Services.Configure<RabbitMqOptions>(ConfigureRabbit);
        return builder.Build();
    }

    private void ConfigureRabbit(RabbitMqOptions o)
    {
        var uri = new Uri(_rabbit.GetConnectionString());
        var parts = uri.UserInfo.Split(':', 2);
        o.Host = uri.Host;
        o.Port = uri.Port;
        o.Username = Uri.UnescapeDataString(parts[0]);
        o.Password = Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty);
        o.Exchange = "dcl.events";
    }

    // All three services share the database, each keeping to its own schema via HasDefaultSchema
    // (the outbox dispatcher qualifies its SQL, so no search_path is needed). A distinct
    // Application Name gives each service — and the verifier — its own connection pool, so the
    // verifier's polling never starves the services' pools.
    private string Conn(string appName) =>
        $"{_postgres.GetConnectionString()};Application Name={appName};Maximum Pool Size=20";

    // `docker pause`/`unpause` freezes the broker without a graceful close, so connections,
    // data and the port mapping all survive — the broker is simply unresponsive in between.
    private async Task DockerAsync(string command, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("docker", $"{command} {_rabbit.Id}")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the docker process.");
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {command} failed (exit {proc.ExitCode}): {await proc.StandardError.ReadToEndAsync(ct)}");
        }
    }

    private static async Task MigrateAsync(DbContext db, CancellationToken ct) => await db.Database.MigrateAsync(ct);

    private async Task SeedStockAsync(CancellationToken ct)
    {
        await using var db = NewInventory();
        db.StockItems.Add(StockItem.Create("SKU-1", InitialStock));
        await db.SaveChangesAsync(ct);
    }

    private OrdersDbContext NewOrders() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(Conn("verifier")).Options);

    private InventoryDbContext NewInventory() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(Conn("verifier")).Options);

    private PaymentsDbContext NewPayments() =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(Conn("verifier")).Options);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, Func<Task<string>> onTimeout, CancellationToken ct)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new TimeoutException($"Condition not met within the allotted time. {await onTimeout()}");
    }

    private async Task<string> DumpAsync(CancellationToken ct)
    {
        await using var od = NewOrders();
        await using var id = NewInventory();
        await using var pd = NewPayments();
        var oOut = await od.Outbox.Select(o => $"{o.Type}:{o.ProcessedAt != null}").ToListAsync(ct);
        var iOut = await id.Outbox.Select(o => $"{o.Type}:{o.ProcessedAt != null}").ToListAsync(ct);
        var pOut = await pd.Outbox.Select(o => $"{o.Type}:{o.ProcessedAt != null}").ToListAsync(ct);
        var saga = await od.Sagas.Select(s => s.State.ToString()).FirstOrDefaultAsync(ct);
        var stock = await id.StockItems.Select(s => (int?)s.Available).FirstOrDefaultAsync(ct);
        return $"[orders saga={saga} outbox=({string.Join(",", oOut)}) | inventory stock={stock} outbox=({string.Join(",", iOut)}) | payments outbox=({string.Join(",", pOut)})]";
    }
}
