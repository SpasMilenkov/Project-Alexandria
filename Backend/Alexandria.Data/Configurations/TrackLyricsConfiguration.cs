using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class TrackLyricsConfiguration : IEntityTypeConfiguration<TrackLyrics>
{
    public void Configure(EntityTypeBuilder<TrackLyrics> builder)
    {
        builder.Property(e => e.PlainLyrics)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(e => e.SyncedLyrics)
            .HasColumnType("text")
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

        builder.HasOne(e => e.TranspilationJob)
            .WithOne(e => e.TrackLyrics);

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Synced-only lyrics are indexed with their LRC tags ([mm:ss.xx], [ar:...]) blanked
        // so timestamps never become searchable tokens or break phrase adjacency.
        builder.Property(e => e.SearchVector)
            .HasComputedColumnSql(
                @"to_tsvector('simple', coalesce(
            nullif(btrim(""PlainLyrics""), ''),
            regexp_replace(coalesce(""SyncedLyrics"", ''), '\[[^\]]*\]', ' ', 'g')
        ))",
                stored: true);

        builder.HasIndex(e => e.SearchVector)
            .HasMethod("gin")
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(e => e.Id);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.JobId).IsUnique();
        builder.HasIndex(e => e.TranspilationJobId)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.ToTable("TrackLyrics");
    }
}