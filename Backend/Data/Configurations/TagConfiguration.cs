using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;
using File = Models.File;

namespace Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Name)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();
        
        builder.Property(e => e.OwnerId)
            .IsRequired();
        
        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(ValidationConstants.StringLengths.UserId)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.UserId})")
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
        
        // Relations
        builder.HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Many-to-many relationship with Files
        builder.HasMany(t => t.Files)
            .WithMany(f => f.Tags)
            .UsingEntity<Dictionary<string, object>>(
                "FileTags",
                j => j.HasOne<File>().WithMany().HasForeignKey("FileId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("FileId", "TagId");
                    j.ToTable("FileTags");
                });
        
        // Indexes for performance
        builder.HasIndex(e => e.OwnerId);
        builder.HasIndex(e => e.Name);  
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => new { e.OwnerId, e.Name })
            .IsUnique();
        
        // Table name
        builder.ToTable("Tags");
    }
}