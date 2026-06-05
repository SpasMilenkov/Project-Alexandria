using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class DirectoryPolicyConfiguration : IEntityTypeConfiguration<DirectoryPolicy>
{
    public void Configure(EntityTypeBuilder<DirectoryPolicy> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DirectoryId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.InheritedByChildren)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasOne(p => p.Directory)
            .WithOne()
            .HasForeignKey<DirectoryPolicy>(p => p.DirectoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Rules)
            .WithOne(r => r.Policy)
            .HasForeignKey(r => r.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        // One policy per directory, enforced at the DB level.
        builder.HasIndex(e => e.DirectoryId)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(e => e.DeletedAt);

        builder.ToTable("DirectoryPolicies");
    }
}