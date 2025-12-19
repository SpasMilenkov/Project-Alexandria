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
            .HasColumnType("uuid")
            .IsRequired(false);

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
        builder.HasIndex(f => f.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        // Table name
        builder.ToTable("Files");
    }
}