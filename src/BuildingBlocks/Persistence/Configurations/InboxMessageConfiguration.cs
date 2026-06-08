using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox");

        // PK on the message-id is the deduplication lock (ADR-002).
        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.MessageId).HasColumnName("message_id");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
    }
}
