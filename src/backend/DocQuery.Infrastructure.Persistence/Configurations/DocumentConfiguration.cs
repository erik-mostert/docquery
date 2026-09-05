using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocQuery.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).ValueGeneratedNever();

        builder.Property(document => document.FileName).HasMaxLength(260).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(document => document.SizeInBytes).IsRequired();
        builder.Property(document => document.BlobName).HasMaxLength(100).IsRequired();
        builder.HasIndex(document => document.BlobName).IsUnique();
        builder.Property(document => document.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(document => document.UploadedAt).IsRequired();
        builder.Property(document => document.ChunkCount);
        builder.Property(document => document.ChunkedAt);
        builder.Property(document => document.FailureReason).HasMaxLength(Document.MaxFailureReasonLength);

        builder.Ignore(document => document.DomainEvents);
        builder.Ignore(document => document.CanBeChunked);
    }
}
