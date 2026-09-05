using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DocQuery.Infrastructure.Persistence;

public sealed class DocQueryDbContext(DbContextOptions<DocQueryDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocQueryDbContext).Assembly);
    }
}
