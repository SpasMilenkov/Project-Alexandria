using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;
using File = Models.File;

namespace Data.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<File>
{
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
        builder.HasOne(f => f.Preview)
            .WithOne(p => p.File)
            .HasForeignKey<Preview>(p => p.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Tags)
            .WithMany(t => t.Files)
            .UsingEntity<Dictionary<string, object>>(
                "FileTags", // Join table name
                j => j.HasOne<Tag>().WithMany().HasForeignKey("TagId"),
                j => j.HasOne<File>().WithMany().HasForeignKey("FileId"),
                j =>
                {
                    j.HasKey("FileId", "TagId");
                    j.ToTable("FileTags");
                });

        builder.Property(e => e.CurrentVersionId)
            .IsRequired(false);

        builder.HasOne(f => f.CurrentVersion)
            .WithMany()
            .HasForeignKey(f => f.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // FileConfiguration
        builder.HasOne(f => f.CurrentVersion)
            .WithMany()
            .HasForeignKey(f => f.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict)
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

        // Indexes for performance
        builder.HasIndex(e => e.CreatedAt);

        builder.HasIndex(e => e.OwnerId);

        builder.HasIndex(e => e.NormalizedName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        // Table name
        builder.ToTable("Files");
    }
}