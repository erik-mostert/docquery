using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DocQuery.Infrastructure.Persistence.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [DockerFact]
    public async Task All_migrations_are_applied_and_model_has_no_pending_changes()
    {
        await using var context = postgres.CreateContext();

        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges(), "The model differs from the last migration; add a migration.");
    }
}
