using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;
using Directory = Models.Directory;

namespace Data.Configurations;

public class DirectoryConfiguration: IEntityTypeConfiguration<Directory>
{
    public void Configure(EntityTypeBuilder<Directory> builder)
    {
        builder.Property(e => e.Name)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})");
        
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
        
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        
        
        //Index to ensure that a parent can't have a child directory with the same name
        builder.HasIndex(e => new { e.ParentId, e.Name }).IsUnique();
        
        builder.ToTable("Directories");
    }

}