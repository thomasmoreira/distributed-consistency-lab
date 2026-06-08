namespace Services.Payments;

/// <summary>
/// Placeholder for the Payments idempotent consumer (spec §10, phase 4).
/// dotnet-dev will wire this to RabbitMQ: consume StockReserved -> inbox check ->
/// charge payment -> emit PaymentCharged/PaymentFailed via the outbox.
/// </summary>
public sealed partial class PaymentsConsumerHost(ILogger<PaymentsConsumerHost> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Payments consumer host started (not yet wired to RabbitMQ).")]
    private static partial void LogStarted(ILogger logger);
}
