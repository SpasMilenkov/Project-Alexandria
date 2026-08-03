using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class FileTagConfiguration : IEntityTypeConfiguration<FileTag>
{
    public void Configure(EntityTypeBuilder<FileTag> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FileId)
            .IsRequired();

        builder.Property(e => e.TagId)
            .IsRequired();

        builder.Property(e => e.Source)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.Confidence)
            .HasColumnType("double precision")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // Relations
        builder.HasOne(ft => ft.File)
            .WithMany(f => f.FileTags)
            .HasForeignKey(ft => ft.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ft => ft.Tag)
            .WithMany(t => t.FileTags)
            .HasForeignKey(ft => ft.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        // One association per (File, Tag) pair, source dictates priority in app logic
        builder.HasIndex(e => new { e.FileId, e.TagId })
            .IsUnique();
        builder.HasIndex(e => e.FileId);
        builder.HasIndex(e => e.TagId);
        builder.HasIndex(e => e.Source);

        builder.ToTable("FileTags");
    }
}