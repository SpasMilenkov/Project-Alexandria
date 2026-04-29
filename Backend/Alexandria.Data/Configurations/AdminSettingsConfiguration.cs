using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class AdminSettingsConfiguration : IEntityTypeConfiguration<AdminSettings>
{
    public void Configure(EntityTypeBuilder<AdminSettings> builder)
    {
        builder.Property(e => e.Value)
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);


        builder.HasIndex(e => e.Key)
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.ToTable("AdminSettings");
    }
}