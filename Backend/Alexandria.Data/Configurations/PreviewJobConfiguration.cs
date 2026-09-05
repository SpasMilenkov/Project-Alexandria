using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class PreviewJobConfiguration : IEntityTypeConfiguration<PreviewJob>
{
    public void Configure(EntityTypeBuilder<PreviewJob> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Version)
            .WithMany()
            .HasForeignKey(e => e.VersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.JobId).IsUnique();
        builder.HasIndex(e => new { e.VersionId, e.Kind, e.UserId }).IsUnique();
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("PreviewJobs");
    }
}