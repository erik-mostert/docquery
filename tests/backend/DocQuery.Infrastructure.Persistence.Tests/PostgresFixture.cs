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

    /// <summary>Creates an empty database in the container and returns a connection string for it.</summary>
    public async Task<string> CreateFreshDatabaseAsync()
    {
        var name = FormattableString.Invariant($"fresh_{Guid.NewGuid():N}");
        await using (var connection = new Npgsql.NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = FormattableString.Invariant($"CREATE DATABASE \"{name}\"");
            await command.ExecuteNonQueryAsync();
        }

        return new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public DocQueryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DocQueryDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Builds the persistence services exactly as the host registers them, against the container.</summary>
    public ServiceProvider CreateServices(Action<IServiceCollection>? customise = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DocQueryDbContext>(options => options.UseNpgsql(ConnectionString));
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
