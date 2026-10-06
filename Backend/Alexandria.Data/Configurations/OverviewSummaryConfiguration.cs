using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class OverviewSummaryConfiguration : IEntityTypeConfiguration<OverviewSummary>
{
    public void Configure(EntityTypeBuilder<OverviewSummary> builder)
    {
        builder.ToTable("OverviewSummaries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(x => x.PeriodStart)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PeriodEnd)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.GeneratedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.FinalizedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(x => x.SchemaVersion)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(x => x.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(x => x.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.Kind, x.PeriodStart, x.PeriodEnd })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
