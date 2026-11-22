using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class FileDraftConfiguration : IEntityTypeConfiguration<FileDraft>
{
    public void Configure(EntityTypeBuilder<FileDraft> builder)
    {
        builder.HasKey(e => e.FileId);

        builder.Property(e => e.BaseVersionId)
            .IsRequired();

        builder.Property(e => e.Revision)
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
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

        // No foreign key to the base version: a draft whose version is gone is still the owner's
        // text, and opening the note settles it as a conflict.
        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // The sweep looks for drafts nobody has written to for a while.
        builder.HasIndex(e => e.UpdatedAt);

        builder.ToTable("FileDrafts");
    }
}
