using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocQuery.Infrastructure.Persistence.Configurations;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(chunk => chunk.Id);
        builder.Property(chunk => chunk.Id).ValueGeneratedNever();

        // Foreign key without a navigation: the aggregate never loads its chunks.
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(chunk => chunk.Position).IsRequired();
        builder.Property(chunk => chunk.PageNumber).IsRequired();
        builder.Property(chunk => chunk.Text).HasColumnType("text").IsRequired();
        builder.Property(chunk => chunk.TokenCount).IsRequired();

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.Position }).IsUnique();
    }
}
