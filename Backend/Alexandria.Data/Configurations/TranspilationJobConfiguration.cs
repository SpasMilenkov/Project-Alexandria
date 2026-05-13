using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class TranspilationJobConfiguration : IEntityTypeConfiguration<TranspilationJob>
{
    public void Configure(EntityTypeBuilder<TranspilationJob> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.ProgressPercent)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.ErrorDetail)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.LongString})")
            .IsRequired(false);

        builder.Property(e => e.IsVideo)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasOne(e => e.ContentObject)
            .WithOne()
            .HasForeignKey<TranspilationJob>(e => e.ContentObjectId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.HasMany(e => e.Representations)
            .WithOne(r => r.Job)
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ContentObjectId).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("TranspilationJobs");
    }
}