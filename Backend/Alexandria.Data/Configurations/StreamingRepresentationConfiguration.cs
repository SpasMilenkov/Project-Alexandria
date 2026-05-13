using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class StreamingRepresentationConfiguration : IEntityTypeConfiguration<StreamingRepresentation>
{
    public void Configure(EntityTypeBuilder<StreamingRepresentation> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Codec)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.Width)
            .IsRequired(false);

        builder.Property(e => e.Height)
            .IsRequired(false);

        builder.Property(e => e.BitrateKbps)
            .IsRequired(false);

        // e.g. "{contentObjectId}/v/1080p_av1" or "{contentObjectId}/a/128kbps_opus"
        builder.Property(e => e.SegmentPrefix)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasOne(e => e.Job)
            .WithMany(j => j.Representations)
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // one row per codec per job, no duplicates
        builder.HasIndex(e => new { e.JobId, e.Codec }).IsUnique();
        builder.HasIndex(e => e.Status);

        builder.ToTable("StreamRepresentations");
    }
}