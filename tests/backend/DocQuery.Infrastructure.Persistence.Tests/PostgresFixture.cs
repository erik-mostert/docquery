using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DocQuery.Infrastructure.Persistence.Tests;

/// <summary>One migrated PostgreSQL container (pgvector image) shared by the persistence tests.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    /// <summary>The instant the persistence services see as "now", so outbox timestamps are predictable.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return;
        }

        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Creates a new database in the container, optionally migrated, and returns its connection string.</summary>
    public async Task<string> CreateFreshDatabaseAsync(bool migrate = false)
    {
        var name = FormattableString.Invariant($"fresh_{Guid.NewGuid():N}");
        await using (var connection = new Npgsql.NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = FormattableString.Invariant($"CREATE DATABASE \"{name}\"");
            await command.ExecuteNonQueryAsync();
        }

        var fresh = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
        if (migrate)
        {
            await using var context = CreateContext(fresh);
            await context.Database.MigrateAsync();
        }

        return fresh;
    }

    public static DocQueryDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<DocQueryDbContext>().UseNpgsql(connectionString, npgsql => npgsql.UseVector()).Options);

    public DocQueryDbContext CreateContext() => CreateContext(ConnectionString);

    /// <summary>Builds the persistence services exactly as the host registers them, against the container.</summary>
    public ServiceProvider CreateServices(Action<IServiceCollection>? customise = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DocQueryDbContext>(options => options.UseNpgsql(ConnectionString, npgsql => npgsql.UseVector()));
        services.AddPersistenceServices();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        customise?.Invoke(services);
        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
