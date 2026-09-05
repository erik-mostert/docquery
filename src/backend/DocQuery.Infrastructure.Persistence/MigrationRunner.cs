using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Persistence;

/// <summary>Applies pending migrations before the host accepts traffic.</summary>
internal sealed class MigrationRunner(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DocQueryDbContext>();

        // The Npgsql provider does not implement the history-table existence check (ExistsAsync always returns
        // true); MigrateAsync therefore SELECTs from __EFMigrationsHistory and, on a fresh database, logs the
        // failure at Error level before creating the table. Creating it up front with the provider's idempotent
        // CREATE TABLE IF NOT EXISTS script keeps the first start free of false errors and is a no-op afterwards.
        var history = context.GetService<IHistoryRepository>();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
