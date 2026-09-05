using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class TranspilationJobConfiguration : IEntityTypeConfiguration<TranspilationJob>
{
    public void Configure(EntityTypeBuilder<TranspilationJob> builder)
    {
        builder.HasKey(e => e.Id);


        builder.Property(e => e.IsVideo)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(e => e.FileVersion)
            .WithMany()
            .HasForeignKey(e => e.VersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.SegmentPrefix)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.HasMany(e => e.Representations)
            .WithOne(r => r.Job)
            .HasForeignKey(r => r.TranspilationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.VersionId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(t => t.JobId).IsUnique();

        builder.ToTable("TranspilationJobs");
    }
}