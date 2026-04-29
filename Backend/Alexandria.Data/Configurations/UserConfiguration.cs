using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(e => e.Name)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasIndex(e => e.CreatedAt);
    }
}