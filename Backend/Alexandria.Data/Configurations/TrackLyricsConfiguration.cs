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

        builder.HasIndex(e => e.Id);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.JobId).IsUnique();
        builder.HasIndex(e => e.TranspilationJobId)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.ToTable("TrackLyrics");
    }
}