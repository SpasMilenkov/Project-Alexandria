using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class DocumentTextConfiguration : IEntityTypeConfiguration<DocumentText>
{
    public void Configure(EntityTypeBuilder<DocumentText> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnType("text")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(e => e.SearchVector)
            .HasColumnType("tsvector")
            .IsRequired();

        builder.Property(e => e.CharacterCount)
            .IsRequired();

        builder.Property(e => e.IsTruncated)
            .IsRequired();

        builder.Property(e => e.ErrorDetail)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.LongString})")
            .IsRequired(false);

        builder.Property(e => e.AttemptCount)
            .IsRequired();

        builder.Property(e => e.LastAttemptAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.ExtractorVersion)
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

        builder.HasOne(e => e.ContentObject)
            .WithOne(c => c.DocumentText)
            .HasForeignKey<DocumentText>(e => e.ContentObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ContentObjectId).IsUnique();

        builder.HasIndex(e => e.SearchVector)
            .HasMethod("gin")
            .HasFilter("\"DeletedAt\" IS NULL");

        // The admin overview counts rows per status, and the sweep looks for failed rows to retry.
        builder.HasIndex(e => e.Status);

        builder.ToTable("DocumentText");
    }
}