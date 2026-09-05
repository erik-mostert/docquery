using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocQuery.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(500).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.ProcessedAt);
        builder.Property(message => message.Error).HasMaxLength(4000);

        // The relay scans for unprocessed rows in order of occurrence.
        builder.HasIndex(message => new { message.ProcessedAt, message.OccurredAt });
    }
}
