using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Data.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<File>
{
    private const string GalleryIndexFilter = "\"GalleryDate\" IS NOT NULL AND \"DeletedAt\" IS NULL";

    public void Configure(EntityTypeBuilder<File> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.MimeType)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.NormalizedName)
            .HasComputedColumnSql(
                @"regexp_replace(
            lower(
                regexp_replace(
                    regexp_replace(
                        coalesce(""Name"", ''),
                        '\.[^.]*$', ''
                    ),
                    '[_\-()[\]]+', ' ', 'g'
                )
            ),
            '\s+', ' ', 'g'
        )",
                stored: true);

        builder.Property(e => e.SearchVector)
            .HasComputedColumnSql(
                @"to_tsvector('simple',
                regexp_replace(
                    regexp_replace(coalesce(""Name"", ''), '\.[^.]*$', ''),
                    '[_\-()[\]]+', ' ', 'g'
                )
            )",
                stored: true);
        // Relations
        builder.Property(e => e.CurrentVersionId)
            .IsRequired(false);

        builder.HasOne(f => f.CurrentVersion)
            .WithMany()
            .HasForeignKey(f => f.CurrentVersionId)
            .OnDelete(DeleteBehavior.SetNull)
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

        builder.Property(e => e.GalleryDate)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false)
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(e => e.GalleryDate).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(e => e.GalleryDate).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        // Indexes for performance
        builder.HasIndex(e => e.CreatedAt);

        builder.HasIndex(e => e.OwnerId);

        // Keyset pages read these in order, so each page is one index range scan. The filter
        // matches the gallery query exactly, which is what lets Postgres pick the partial index.
        builder.HasIndex(e => new { e.OwnerId, e.GalleryDate, e.Id }, "IX_Files_Gallery_Taken")
            .HasFilter(GalleryIndexFilter);

        builder.HasIndex(e => new { e.OwnerId, e.CreatedAt, e.Id }, "IX_Files_Gallery_Uploaded")
            .HasFilter(GalleryIndexFilter);

        builder.HasIndex(e => e.NormalizedName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        // Table name
        builder.ToTable("Files");
    }
}