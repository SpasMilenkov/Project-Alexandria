using Common;
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

        builder.Property(e => e.Path)
            .HasMaxLength(ValidationConstants.StringLengths.ExtraLongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ExtraLongString})")
            .IsRequired();

        builder.Property(e => e.MimeType)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(ValidationConstants.StringLengths.UserId)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.UserId})")
            .IsRequired(false);

        // BigInteger for file size - using numeric for PostgreSQL
        builder.Property(e => e.Size)
            .HasColumnType("numeric(20,0)")
            .IsRequired();

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

        // Table name
        builder.ToTable("Files");
    }
}