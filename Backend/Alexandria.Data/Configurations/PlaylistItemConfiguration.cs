using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class PlaylistItemConfiguration : IEntityTypeConfiguration<PlaylistItem>
{
    public void Configure(EntityTypeBuilder<PlaylistItem> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Position)
            .IsRequired();

        builder.Property(e => e.PlaylistId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.TranspilationJobId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

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

        // Relationships
        builder.HasOne(e => e.Playlist)
            .WithMany(p => p.PlaylistItems)
            .HasForeignKey(e => e.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TranspilationJob)
            .WithMany()
            .HasForeignKey(e => e.TranspilationJobId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.PlaylistId);
        builder.HasIndex(e => new { e.PlaylistId, e.Position });
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("PlaylistItems");
    }
}