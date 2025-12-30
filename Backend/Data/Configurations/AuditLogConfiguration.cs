using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;

namespace Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        // Convert OperationType enum to string in database
        builder.Property(a => a.OperationType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        // Convert EntityType enum to string in database
        builder.Property(a => a.EntityType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();

        // Store MetadataJson as JSON column type
        builder.Property(a => a.MetadataJson)
            .HasColumnType("json")
            .IsRequired(false);

        builder.Property(a => a.Description)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .IsRequired();
        
        builder.Property(a => a.Source)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .IsRequired();
        
        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.UserId).IsRequired(false);

        // Optional: Add index for common queries
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}