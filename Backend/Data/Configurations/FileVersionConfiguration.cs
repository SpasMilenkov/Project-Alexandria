using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;
using File = Models.File;

namespace Data.Configurations;

public class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.Property(e => e.MimeType)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();
        
        builder.Property(e => e.ContentHash)
            .IsRequired()
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasKey(e => e.Id);
        
        // BigInteger for file size - using numeric for PostgreSQL
        builder.Property(e => e.Size)
            .HasColumnType("numeric(20,0)")
            .IsRequired();

        builder.Property(e => e.VersionNumber)
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

        //Relations
        builder.HasOne(e => e.File)
            .WithOne(f => f.CurrentVersion)
            .HasForeignKey<File>(f => f.CurrentVersionId);
        
        // Indexes for performance
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ContentHash);
        
        builder.ToTable("FileVersions");
    }
}