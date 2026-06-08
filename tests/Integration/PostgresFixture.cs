using Npgsql;

namespace Tests.Integration;

/// <summary>
/// One shared PostgreSQL container for the whole integration suite. Each test gets a fresh,
/// isolated database on that server (via <see cref="CreateDatabaseAsync"/>) instead of
/// spinning its own container — far less container churn, so no transient start flakiness.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly Testcontainers.PostgreSql.PostgreSqlContainer _container =
        new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a unique database on the shared server and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken ct = default)
    {
        var database = "t" + Guid.NewGuid().ToString("N");

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync(ct);
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{database}\"";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;
    }
}

/// <summary>Binds all integration tests to the single shared <see cref="PostgresFixture"/>.</summary>
[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "xUnit collection-definition convention.")]
public sealed class IntegrationCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "integration";
}
