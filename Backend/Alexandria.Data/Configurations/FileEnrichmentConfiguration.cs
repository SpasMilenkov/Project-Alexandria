using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class FileEnrichmentConfiguration : IEntityTypeConfiguration<FileEnrichment>
{
    public void Configure(EntityTypeBuilder<FileEnrichment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FileId)
            .IsRequired();

        builder.Property(e => e.Analyzer)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.Version)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
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

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // Relations
        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        // One enrichment row per (File, Analyzer, ModelVersion): idempotent append-only history
        builder.HasIndex(e => new { e.FileId, e.Analyzer, e.Version }).IsUnique();
        // Fetch latest result per analyzer for a file without a table scan
        builder.HasIndex(e => new { e.FileId, e.Analyzer, e.CreatedAt });

        builder.ToTable("FileEnrichments");
    }
}