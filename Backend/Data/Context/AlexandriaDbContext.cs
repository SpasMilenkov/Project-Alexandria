using Data.Configurations;
using Data.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models;
using File = Models.File;
using Directory = Models.Directory;

namespace Data.Context;

public class AlexandriaDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // Constructor for DI (used at runtime)
    public AlexandriaDbContext(
        DbContextOptions<AlexandriaDbContext> options,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Constructor for migrations (when IHttpContextAccessor is not available)
    public AlexandriaDbContext(DbContextOptions<AlexandriaDbContext> options)
        : base(options)
    {
        _httpContextAccessor = null;
    }

    public DbSet<File> Files { get; set; }
    public DbSet<SignedUrl> SignedUrls { get; set; }
    public DbSet<MediaMetadata> MediaMetadata { get; set; }
    public DbSet<Preview> Previews { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Directory> Directories { get; set; }
    public DbSet<FileVersion> FileVersions { get; set; }
    public DbSet<ContentObject> ContentObjects { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Upload> Uploads { get; set; }
    public DbSet<UserSettings> UserSettings { get; set; }
    public DbSet<AdminSettings> AdminSettings { get; set; }

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
                entity.Property<Guid?>("UpdatedBy")
                    .HasMaxLength(100);
            }
        }

        modelBuilder.ApplyConfiguration(new FileConfiguration());
        modelBuilder.ApplyConfiguration(new SignedUrlConfiguration());
        modelBuilder.ApplyConfiguration(new PreviewConfiguration());
        modelBuilder.ApplyConfiguration(new MediaMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new DirectoryConfiguration());
        modelBuilder.ApplyConfiguration(new FileVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ContentObjectConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new UploadConfiguration());
        modelBuilder.ApplyConfiguration(new UserSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AdminSettingsConfiguration());
    }   

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only add interceptor if we have HttpContext (not during migrations)
        if (_httpContextAccessor != null)
        {
            optionsBuilder.AddInterceptors(new AuditInterceptor(_httpContextAccessor));
        }
    }
}