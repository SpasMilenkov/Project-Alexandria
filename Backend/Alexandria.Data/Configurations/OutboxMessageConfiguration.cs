using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(e => e.Id);

        // AMQP caps routing keys at 255 bytes.
        builder.Property(e => e.RoutingKey)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .IsRequired();

        builder.Property(e => e.Body)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(e => e.JobId)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.AvailableAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Attempts)
            .IsRequired();

        builder.Property(e => e.LastError)
            .HasMaxLength(ValidationConstants.StringLengths.ExtraLongString)
            .IsRequired(false);

        builder.Property(e => e.SentAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeadAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // The relay polls every few seconds and only ever reads rows still waiting to be sent,
        // so the index stays as small as the backlog.
        builder.HasIndex(e => e.AvailableAt)
            .HasFilter("\"SentAt\" IS NULL AND \"DeadAt\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");

        builder.HasIndex(e => e.SentAt)
            .HasFilter("\"SentAt\" IS NOT NULL")
            .HasDatabaseName("IX_OutboxMessages_Sent");

        builder.HasIndex(e => e.DeadAt)
            .HasFilter("\"DeadAt\" IS NOT NULL")
            .HasDatabaseName("IX_OutboxMessages_Dead");

        builder.ToTable("OutboxMessages");
    }
}
