// These tests each spin real Postgres/RabbitMQ containers; running them in parallel
// oversubscribes Docker and makes timing-sensitive tests (broker pause/unpause) flaky.
// Run them one at a time — slower, but reliable.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
