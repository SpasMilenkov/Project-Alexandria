using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class NoteLinkScanConfiguration : IEntityTypeConfiguration<NoteLinkScan>
{
    public void Configure(EntityTypeBuilder<NoteLinkScan> builder)
    {
        builder.HasKey(e => e.ContentObjectId);

        builder.Property(e => e.ScannerVersion)
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

        // Content that is cleaned up takes its scan with it.
        builder.HasOne(e => e.ContentObject)
            .WithOne()
            .HasForeignKey<NoteLinkScan>(e => e.ContentObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Links)
            .WithOne(l => l.Scan)
            .HasForeignKey(l => l.ContentObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("NoteLinkScans");
    }
}
