using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Tests.Integration;

/// <summary>
/// Proves exactly-once-effect at the inbox-processor level, deterministically (no broker):
/// processing the same message-id twice runs the effect once (ADR-002).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class InboxProcessorTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string _conn = null!;

    public async Task InitializeAsync()
    {
        _conn = await postgres.CreateDatabaseAsync();

        await using var db = NewDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
            .UseNpgsql(_conn)
            .Options;
        return new MessagingTestDbContext(options);
    }
}
