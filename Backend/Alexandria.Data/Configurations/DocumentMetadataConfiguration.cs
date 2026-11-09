using System.Linq.Expressions;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class DocumentMetadataConfiguration : IEntityTypeConfiguration<DocumentMetadata>
{
    private const int IsbnLength = 13;

    public void Configure(EntityTypeBuilder<DocumentMetadata> builder)
    {
        builder.HasKey(e => e.Id);

        ConfigureText(builder, e => e.Title, ValidationConstants.StringLengths.LongString);
        ConfigureText(builder, e => e.Subject, ValidationConstants.StringLengths.LongString);
        ConfigureText(builder, e => e.Keywords, ValidationConstants.StringLengths.LongString);
        ConfigureText(builder, e => e.Language, ValidationConstants.StringLengths.ShortString);
        ConfigureText(builder, e => e.Author, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.LastModifiedBy, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.Company, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.Application, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.Producer, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.PaperSize, ValidationConstants.StringLengths.ShortString);
        ConfigureText(builder, e => e.Publisher, ValidationConstants.StringLengths.MediumString);
        ConfigureText(builder, e => e.Isbn, IsbnLength);
        ConfigureText(builder, e => e.Series, ValidationConstants.StringLengths.MediumString);

        builder.Property(e => e.SeriesIndex)
            .HasPrecision(8, 2)
            .IsRequired(false);

        builder.Property(e => e.IsDrmProtected)
            .IsRequired();

        builder.Property(e => e.AuthoredAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.ModifiedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.IsPasswordProtected)
            .IsRequired();

        builder.Property(e => e.RawJson)
            .HasColumnType("jsonb")
            .IsRequired();

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

        builder.HasOne(e => e.FileVersion)
            .WithOne(v => v.DocumentMetadata)
            .HasForeignKey<DocumentMetadata>(e => e.FileVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.FileVersionId).IsUnique();
        builder.HasIndex(e => e.AuthoredAt);
        builder.HasIndex(e => e.Isbn);

        builder.ToTable("DocumentMetadata");
    }

    private static void ConfigureText(
        EntityTypeBuilder<DocumentMetadata> builder,
        Expression<Func<DocumentMetadata, string?>> property,
        int maxLength)
    {
        builder.Property(property)
            .HasMaxLength(maxLength)
            .HasColumnType($"varchar({maxLength})")
            .IsRequired(false);
    }
}
