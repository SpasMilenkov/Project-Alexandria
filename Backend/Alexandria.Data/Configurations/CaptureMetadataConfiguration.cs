using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class CaptureMetadataConfiguration : IEntityTypeConfiguration<CaptureMetadata>
{
    public void Configure(EntityTypeBuilder<CaptureMetadata> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CapturedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        ConfigureText(builder, e => e.CameraMake);
        ConfigureText(builder, e => e.CameraModel);
        ConfigureText(builder, e => e.LensModel);
        ConfigureText(builder, e => e.Software);

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
            .WithOne()
            .HasForeignKey<CaptureMetadata>(e => e.FileVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.FileVersionId).IsUnique();
        builder.HasIndex(e => e.CapturedAt);

        builder.ToTable("CaptureMetadata");
    }

    private static void ConfigureText(
        EntityTypeBuilder<CaptureMetadata> builder,
        System.Linq.Expressions.Expression<Func<CaptureMetadata, string?>> property)
    {
        builder.Property(property)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired(false);
    }
}
