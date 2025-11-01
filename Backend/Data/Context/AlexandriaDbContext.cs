using Data.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models;
using File = Models.File;

namespace Data.Context;

public class AlexandriaDbContext(DbContextOptions<AlexandriaDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<File> Files { get; set; }
    public DbSet<SignedUrl> SignedUrls { get; set; }
    public DbSet<MediaMetadata> MediaMetadata { get; set; }
    public DbSet<Preview> Previews { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
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

        modelBuilder.ApplyConfiguration(new FileConfiguration());
        modelBuilder.ApplyConfiguration(new SignedUrlConfiguration());
        modelBuilder.ApplyConfiguration(new PreviewConfiguration());
        modelBuilder.ApplyConfiguration(new MediaMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}