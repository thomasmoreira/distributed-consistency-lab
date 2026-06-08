namespace Services.Inventory;

/// <summary>
/// Placeholder for the Inventory idempotent consumer (spec §10, phase 3).
/// dotnet-dev will wire this to RabbitMQ: consume OrderPlaced -> inbox check ->
/// reserve stock -> emit StockReserved/StockReservationFailed via the outbox.
/// </summary>
public sealed partial class InventoryConsumerHost(ILogger<InventoryConsumerHost> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inventory consumer host started (not yet wired to RabbitMQ).")]
    private static partial void LogStarted(ILogger logger);
}
