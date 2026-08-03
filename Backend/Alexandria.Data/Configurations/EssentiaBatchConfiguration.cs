using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class EssentiaBatchConfiguration : IEntityTypeConfiguration<EssentiaBatch>
{
    public void Configure(EntityTypeBuilder<EssentiaBatch> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Backbone)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.DispatchedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasMany(e => e.Files)
            .WithOne(f => f.Batch)
            .HasForeignKey(f => f.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Backbone);
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("EssentiaBatches");
    }
}