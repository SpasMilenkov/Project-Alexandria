using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class OperationalEventConfiguration : IEntityTypeConfiguration<OperationalEvent>
{
    public void Configure(EntityTypeBuilder<OperationalEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.ServiceType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Code)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(e => e.CreatedBy)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);
        builder.Property(e => e.ResolvedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasIndex(e => new { e.ServiceType, e.Status });

        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("OperationalEvents");
    }
}