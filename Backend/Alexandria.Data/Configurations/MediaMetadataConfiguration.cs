using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class MediaMetadataConfiguration : IEntityTypeConfiguration<MediaMetadata>
{
    public void Configure(EntityTypeBuilder<MediaMetadata> builder)
    {
        builder.HasKey(e => e.Id);

        // File information
        builder.Property(e => e.Duration)
            .IsRequired();

        builder.Property(e => e.BitrateMbps)
            .IsRequired();

        builder.Property(e => e.FormatName)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired(false);

        builder.Property(e => e.ThumbnailPath)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);
        // Stream information
        builder.Property(e => e.VideoCodec)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired(false);

        builder.Property(e => e.AudioCodec)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired(false);

        builder.Property(e => e.Width)
            .IsRequired();

        builder.Property(e => e.Height)
            .IsRequired();

        builder.Property(e => e.HasAudio)
            .IsRequired();

        // Audio metadata tags
        builder.Property(e => e.Title)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.Property(e => e.Artist)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.Property(e => e.Album)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.Property(e => e.Year)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired(false);

        builder.Property(e => e.Genre)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        // DateTime properties
        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasOne(m => m.File)
            .WithOne(f => f.MediaMetadata)
            .HasForeignKey<MediaMetadata>(m => m.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.NormalizedSearch)
            .HasComputedColumnSql(
                @"lower(
            coalesce(""Title"", '') || ' ' ||
            coalesce(""Artist"", '') || ' ' ||
            coalesce(""Album"", '') || ' ' ||
            coalesce(""Genre"", '')
        )",
                stored: true);

        builder.Property(e => e.SearchVector)
            .HasComputedColumnSql(
                @"setweight(to_tsvector('simple', coalesce(""Title"", '')),  'A') ||
          setweight(to_tsvector('simple', coalesce(""Artist"", '')), 'B') ||
          setweight(to_tsvector('simple', coalesce(""Album"", '')),  'C') ||
          setweight(to_tsvector('simple', coalesce(""Genre"", '')),  'D')",
                stored: true);

        builder.HasIndex(e => e.SearchVector)
            .HasMethod("gin");

        builder.HasIndex(e => e.NormalizedSearch)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.FileId);

        builder.ToTable("MediaMetadata");
    }
}