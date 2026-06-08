using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Tests.Integration;

/// <summary>
/// Proves exactly-once-effect at the inbox-processor level, deterministically (no broker):
/// processing the same message-id twice runs the effect once (ADR-002).
/// </summary>
public sealed class InboxProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Effect_runs_once_when_the_same_message_is_processed_twice()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var ct = cts.Token;

        var messageId = Guid.CreateVersion7();
        var runs = 0;
        Task Effect(CancellationToken _)
        {
            runs++;
            return Task.CompletedTask;
        }

        bool firstRan;
        await using (var db = NewDb())
        {
            firstRan = await new EfInboxProcessor(db).ProcessOnceAsync(messageId, Effect, ct);
        }

        bool secondRan;
        await using (var db = NewDb())
        {
            secondRan = await new EfInboxProcessor(db).ProcessOnceAsync(messageId, Effect, ct);
        }

        firstRan.ShouldBeTrue();
        secondRan.ShouldBeFalse();
        runs.ShouldBe(1);

        await using (var db = NewDb())
        {
            (await db.Inbox.CountAsync(ct)).ShouldBe(1);
        }
    }

    private MessagingTestDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MessagingTestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new MessagingTestDbContext(options);
    }
}
