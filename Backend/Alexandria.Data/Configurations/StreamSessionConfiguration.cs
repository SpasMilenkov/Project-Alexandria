using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class StreamSessionConfiguration : IEntityTypeConfiguration<StreamSession>
{
    public void Configure(EntityTypeBuilder<StreamSession> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.StartPositionSeconds)
            .IsRequired();

        builder.Property(e => e.EndPositionSeconds)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(e => e.ListenedSeconds)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(e => e.ReachedCompletionThreshold)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.EndedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(e => e.StreamHistory)
            .WithMany(h => h.Sessions)
            .HasForeignKey(e => e.StreamHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // useful for the drop-off analysis query: where in files do people stop?
        builder.HasIndex(e => e.StreamHistoryId);
        builder.HasIndex(e => e.ReachedCompletionThreshold);

        builder.ToTable("StreamSession");
    }
}