using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;

namespace Data.Configurations;

public class ContentObjectConfiguration : IEntityTypeConfiguration<ContentObject>
{
    public void Configure(EntityTypeBuilder<ContentObject> builder)
    {
        builder.Property(e => e.StorageKey)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.LongString})")
            .IsRequired();

        //Time variables
        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.OrphanedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder
            .HasOne(co => co.Upload)
            .WithOne()
            .HasForeignKey<ContentObject>(co => co.UploadId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.Hash).IsUnique();

        builder.ToTable("ContentObjects");
    }
}