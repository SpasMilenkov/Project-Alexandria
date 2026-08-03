using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class EssentiaBatchFileConfiguration : IEntityTypeConfiguration<EssentiaBatchFile>
{
    public void Configure(EntityTypeBuilder<EssentiaBatchFile> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BatchId)
            .IsRequired();

        builder.Property(e => e.FileId)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.ErrorDetail)
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasOne(e => e.Batch)
            .WithMany(b => b.Files)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.BatchId);
        builder.HasIndex(e => e.FileId);

        builder.ToTable("EssentiaBatchFiles");
    }
}