using Common;
using Common.Enumerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Models;

namespace Data.Configurations;

public class SignedUrlConfiguration
    : IEntityTypeConfiguration<SignedUrl>
{
    public void Configure(EntityTypeBuilder<SignedUrl> builder)
    {
        builder.HasKey(e => e.Id);

        // String conversion for enum
        var converter = new EnumToStringConverter<FilePermission>();
        builder.Property(e => e.Permission)
            .HasConversion(converter)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(e => e.BucketName)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.ObjectName)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.LongString})")
            .IsRequired();

        builder.Property(e => e.Token)
            .HasMaxLength(ValidationConstants.StringLengths.ExtraLongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ExtraLongString})")
            .IsRequired();

        builder.Property(e => e.CreatorId)
            .HasMaxLength(ValidationConstants.StringLengths.UserId)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.UserId})")
            .IsRequired();

        // Nullable string property
        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(ValidationConstants.StringLengths.UserId)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.UserId})")
            .IsRequired(false);

        // DateTime properties
        builder.Property(e => e.ExpiresAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasOne(s => s.FileInfo)
            .WithMany(f => f.SignedUrls)
            .HasForeignKey(s => s.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.FileId);
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => new { e.BucketName, e.ObjectName }); // Composite index

        builder.ToTable("SignedUrls");
    }
}