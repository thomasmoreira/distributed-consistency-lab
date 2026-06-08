namespace Tests.Integration;

/// <summary>
/// The flagship suite (spec §7). Each test spins RabbitMQ + PostgreSQL via
/// Testcontainers; the broker scenarios StopAsync/StartAsync the container mid-flow.
/// Skipped until the messaging pipeline lands (dotnet-dev, spec §10 phases 1-6).
/// </summary>
public class ExactlyOnceTests
{
    [Fact(Skip = "F1 — implement with messaging pipeline (spec §7.1)")]
    public void When_broker_dies_during_checkout_order_still_completes_exactly_once()
    {
        // Arrange: Postgres + RabbitMQ via Testcontainers; place an order.
        // Act:     start checkout; rabbitMq.StopAsync() mid-flow; wait; StartAsync().
        // Assert:  Order = Confirmed; Payment charged exactly 1x; Stock reserved 1x;
        //          no orphan outbox rows (all ProcessedAt != null).
    }

    // F2 (redelivery absorbed by the inbox) is proved by RabbitMqConsumerHostTests.

    [Fact(Skip = "F4 — payment failure triggers compensation (spec §6)")]
    public void Payment_failure_releases_stock_and_cancels_order()
    {
    }
}
