using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class SignedUrlConfiguration
    : IEntityTypeConfiguration<SignedUrl>
{
    public void Configure(EntityTypeBuilder<SignedUrl> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Token)
            .HasMaxLength(ValidationConstants.StringLengths.ExtraLongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ExtraLongString})")
            .IsRequired();

        builder.Property(e => e.CreatorId)
            .IsRequired();

        // Nullable string property
        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
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

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PinnedVersion)
            .WithMany()
            .HasForeignKey(s => s.FileVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.FileId);
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => e.FileVersionId);

        builder.ToTable("SignedUrls");
    }
}