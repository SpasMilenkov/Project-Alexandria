using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class StreamHistoryConfiguration : IEntityTypeConfiguration<StreamHistory>
{
    public void Configure(EntityTypeBuilder<StreamHistory> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PositionSeconds)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(e => e.Completed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.LastAccessedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // enforces upsert semantics: one resume point per user per file
        builder.HasIndex(e => new { e.UserId, e.FileId }).IsUnique();
        builder.HasIndex(e => e.LastAccessedAt);

        builder.ToTable("StreamHistory");
    }
}