using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

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

        //Encryption properties
        builder.Property(e => e.IsEncrypted)
            .IsRequired()
            .HasDefaultValue(false);
        // AES-GCM nonce — 12 bytes, not 16
        builder.Property(e => e.EncryptionIv)
            .HasColumnType("bytea");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FileEntry_EncryptionIv_Length",
            "\"EncryptionIv\" IS NULL OR octet_length(\"EncryptionIv\") = 12"));

        // Salt for KDF (PBKDF2) — 16 bytes, matches worker output
        builder.Property(e => e.EncryptionSalt)
            .HasColumnType("bytea");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FileEntry_EncryptionSalt_Length",
            "\"EncryptionSalt\" IS NULL OR octet_length(\"EncryptionSalt\") = 16"));

        // AES-GCM auth tag — always 16 bytes
        builder.Property(e => e.IntegrityTag)
            .HasColumnType("bytea");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FileEntry_IntegrityTag_Length",
            "\"IntegrityTag\" IS NULL OR octet_length(\"IntegrityTag\") = 16"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FileEntry_EncryptionFields_Consistency",
            @"(""IsEncrypted"" = false AND ""EncryptionIv"" IS NULL AND ""EncryptionSalt"" IS NULL AND ""IntegrityTag"" IS NULL)
              OR
              (""IsEncrypted"" = true AND ""EncryptionIv"" IS NOT NULL AND ""EncryptionSalt"" IS NOT NULL AND ""IntegrityTag"" IS NOT NULL)"));

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
            .WithMany(f => f.Versions)
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);


        // Indexes for performance
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ContentHash);
        // Every orphan detection query hits (ContentObjectId, DeletedAt) together
        builder.HasIndex(e => new { e.ContentObjectId, e.DeletedAt })
            .HasDatabaseName("ix_fileversions_contentobjectid_deletedat");
        builder.ToTable("FileVersions");
    }
}