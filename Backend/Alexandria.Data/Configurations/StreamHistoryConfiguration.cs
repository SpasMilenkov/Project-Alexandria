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

        builder.Property(e => e.MaxPositionReachedSeconds)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(e => e.TotalListenedSeconds)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(e => e.TimesCompleted)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.LastCompletedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastAccessedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Ignore(e => e.HasCompleted);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Sessions)
            .WithOne(s => s.StreamHistory)
            .HasForeignKey(s => s.StreamHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.FileId }).IsUnique();
        builder.HasIndex(e => e.LastAccessedAt);

        builder.ToTable("StreamHistory");
    }
}