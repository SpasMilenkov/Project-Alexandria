using Alexandria.Common.Audit;
using Alexandria.Data.Configurations;
using Alexandria.Data.Interceptors;
using Alexandria.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using File = Alexandria.Data.Models.File;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Data.Context;

public class AlexandriaDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    private readonly AuditContext? _auditContext;

    // Constructor for DI (used at runtime)
    public AlexandriaDbContext(
        DbContextOptions<AlexandriaDbContext> options,
        AuditContext auditContext,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditContext = auditContext;
    }

    // Constructor for migrations (when IHttpContextAccessor is not available)
    public AlexandriaDbContext(DbContextOptions<AlexandriaDbContext> options)
        : base(options)
    {
        _httpContextAccessor = null;
        _auditContext = null;
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
    public DbSet<TranspilationJob> TranspilationJobs { get; set; }
    public DbSet<StreamHistory> StreamHistories { get; set; }
    public DbSet<StreamingRepresentation> StreamingRepresentations { get; set; }
    public DbSet<StreamSession> StreamSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply common audit column configuration to all entities implementing IBase
#pragma warning disable S3267
        foreach (var entityType in builder.Model.GetEntityTypes())
#pragma warning restore S3267
        {
            if (!typeof(IBase).IsAssignableFrom(entityType.ClrType)) continue;
            var entity = builder.Entity(entityType.ClrType);
            entity.Property<DateTime>("CreatedAt")
                .HasDefaultValueSql("NOW()")
                .IsRequired();
            entity.Property<DateTime?>("UpdatedAt");
            entity.Property<DateTime?>("DeletedAt");
            entity.Property<Guid?>("UpdatedBy")
                .HasMaxLength(100);
        }

        builder.ApplyConfiguration(new FileConfiguration());
        builder.ApplyConfiguration(new SignedUrlConfiguration());
        builder.ApplyConfiguration(new PreviewConfiguration());
        builder.ApplyConfiguration(new MediaMetadataConfiguration());
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new TagConfiguration());
        builder.ApplyConfiguration(new DirectoryConfiguration());
        builder.ApplyConfiguration(new FileVersionConfiguration());
        builder.ApplyConfiguration(new ContentObjectConfiguration());
        builder.ApplyConfiguration(new AuditLogConfiguration());
        builder.ApplyConfiguration(new UploadConfiguration());
        builder.ApplyConfiguration(new UserSettingsConfiguration());
        builder.ApplyConfiguration(new AdminSettingsConfiguration());
        builder.ApplyConfiguration(new StreamHistoryConfiguration());
        builder.ApplyConfiguration(new TranspilationJobConfiguration());
        builder.ApplyConfiguration(new StreamingRepresentationConfiguration());
        builder.ApplyConfiguration(new StreamSessionConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only add interceptor if we have HttpContext (not during migrations)
        if (_httpContextAccessor != null)
        {
            optionsBuilder.AddInterceptors(new AuditInterceptor(_httpContextAccessor, _auditContext));
        }
    }
}