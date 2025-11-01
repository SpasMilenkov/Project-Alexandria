using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Models;

namespace Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(e => e.Name)
            .HasMaxLength(ValidationConstants.StringLengths.MediumString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.MediumString})")
            .IsRequired();
        
        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(ValidationConstants.StringLengths.UserId)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.UserId})")
            .IsRequired(false);

        builder.HasIndex(e => e.CreatedAt);
    }
}