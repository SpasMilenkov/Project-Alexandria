using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OperationType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        builder.Property(a => a.EventCode)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        builder.Property(a => a.FallbackDescription)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .IsRequired(false);

        builder.Property(a => a.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(a => a.Source)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.UserId)
            .IsRequired(false);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.EventCode);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}