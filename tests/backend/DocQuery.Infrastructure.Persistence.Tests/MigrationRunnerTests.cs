using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DocQuery.Infrastructure.Persistence.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class MigrationRunnerTests(PostgresFixture postgres)
{
    [DockerFact]
    public async Task Migrates_a_fresh_database_without_logging_errors()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync();
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        services.AddDbContext<DocQueryDbContext>(options => options.UseNpgsql(connectionString));
        await using var provider = services.BuildServiceProvider();
        var runner = new MigrationRunner(provider);

        await runner.StartAsync(CancellationToken.None);

        await using var context = new DocQueryDbContext(new DbContextOptionsBuilder<DocQueryDbContext>().UseNpgsql(connectionString).Options);
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        var errors = logs.Entries.Where(entry => entry.Level >= LogLevel.Error).Select(entry => entry.Message).ToList();
        Assert.True(errors.Count == 0, "Unexpected error logs:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [DockerFact]
    public async Task Running_twice_is_idempotent()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync();
        var services = new ServiceCollection();
        services.AddDbContext<DocQueryDbContext>(options => options.UseNpgsql(connectionString));
        await using var provider = services.BuildServiceProvider();
        var runner = new MigrationRunner(provider);

        await runner.StartAsync(CancellationToken.None);
        await runner.StartAsync(CancellationToken.None);

        await using var context = new DocQueryDbContext(new DbContextOptionsBuilder<DocQueryDbContext>().UseNpgsql(connectionString).Options);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Category, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, List<(LogLevel, string, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add((logLevel, category, formatter(state, exception)));
                }
            }
        }
    }
}
