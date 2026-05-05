using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
    public void Configure(EntityTypeBuilder<Upload> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.TempObjectKey)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .IsRequired();

        builder.Property(e => e.MimeType)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.Hash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Upload_Hash_Length",
                "octet_length(\"ContentHash\") IN (16, 32, 64)"
            );
        });

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.FinishedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasIndex(e => e.Hash);

        builder.ToTable("Uploads");
    }
}