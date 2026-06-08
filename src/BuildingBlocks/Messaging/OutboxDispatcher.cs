using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Background poller that publishes pending outbox rows (ADR-001). Each tick opens a DI
/// scope, resolves the scoped <see cref="IOutboxProcessor"/>, and drains a batch. A row is
/// only marked processed after <see cref="IEventPublisher"/> confirms the publish, so a
/// crash mid-publish simply re-publishes on the next tick (absorbed downstream by the inbox).
/// </summary>
public sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxDispatcherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, _options.PollInterval, _options.BatchSize);

        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // a poll failure must not kill the dispatcher loop
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogPollFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Drains one batch. Exposed for deterministic, single-shot integration tests.</summary>
    public async Task<int> DrainAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

        var count = await processor.ProcessPendingAsync(_options.BatchSize, publisher.PublishAsync, ct);
        if (count > 0)
        {
            LogDispatched(logger, count);
        }

        return count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher started (poll={PollInterval}, batch={BatchSize}).")]
    private static partial void LogStarted(ILogger logger, TimeSpan pollInterval, int batchSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox dispatched {Count} message(s).")]
    private static partial void LogDispatched(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox poll failed; will retry next tick.")]
    private static partial void LogPollFailed(ILogger logger, Exception ex);
}
