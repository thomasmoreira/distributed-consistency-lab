using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration;

/// <summary>Concrete <see cref="MessagingDbContext"/> for tests that only need outbox/inbox.</summary>
public sealed class MessagingTestDbContext(DbContextOptions<MessagingTestDbContext> options)
    : MessagingDbContext(options);

/// <summary>Thread-safe counter so a scoped consumer can report invocations to the test.</summary>
public sealed class InvocationCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}

/// <summary>Test consumer whose only effect is bumping a shared counter.</summary>
public sealed class CountingConsumer(InvocationCounter counter) : IIntegrationEventConsumer<OrderPlaced>
{
    public Task ConsumeAsync(OrderPlaced message, CancellationToken ct)
    {
        counter.Increment();
        return Task.CompletedTask;
    }
}
