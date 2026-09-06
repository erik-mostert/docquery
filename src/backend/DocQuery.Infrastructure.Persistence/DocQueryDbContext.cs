using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.Persistence.Configurations;
using DocQuery.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DocQuery.Infrastructure.Persistence;

public sealed class DocQueryDbContext(DbContextOptions<DocQueryDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Strongly typed ids are stored as uuid wherever they appear (keys and foreign keys alike).
        configurationBuilder.Properties<DocumentId>().HaveConversion<DocumentIdConverter>();
        configurationBuilder.Properties<DocumentChunkId>().HaveConversion<DocumentChunkIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector; the server must allow-list it (locally the pgvector image does, in Azure the Bicep does).
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocQueryDbContext).Assembly);
    }
}
