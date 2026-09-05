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

        builder.HasOne(e => e.Batch)
            .WithMany(b => b.Files)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.File)
            .WithMany()
            .HasForeignKey(e => e.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.BatchId);
        builder.HasIndex(e => e.FileId);
        builder.HasIndex(e => e.JobId);
        builder.HasIndex(e => e.CreatedAt);

        builder.ToTable("EssentiaBatchFiles");
    }
}