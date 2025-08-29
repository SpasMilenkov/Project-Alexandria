using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Data.Context;

public class AlexandriaDbContext(DbContextOptions<AlexandriaDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    
        // Apply common audit column configuration to all entities implementing IBase
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IBase).IsAssignableFrom(entityType.ClrType))
            {
                var entity = modelBuilder.Entity(entityType.ClrType);
                entity.Property<DateTime>("CreatedAt")
                    .HasDefaultValueSql("NOW()")
                    .IsRequired();
                entity.Property<DateTime?>("UpdatedAt");
                entity.Property<DateTime?>("DeletedAt");
                entity.Property<string?>("UpdatedBy")
                    .HasMaxLength(100);
            }
        }
    }
}