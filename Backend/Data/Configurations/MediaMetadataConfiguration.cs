using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;

namespace Data.Configurations;

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
            .WithOne()
            .HasForeignKey<MediaMetadata>(m => m.FileId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.FileId);
        
        builder.ToTable("MediaMetadata");
    }
}