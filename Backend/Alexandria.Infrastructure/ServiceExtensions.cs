using Alexandria.Common;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data;
using Alexandria.Repositories;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Archives;
using Alexandria.Services.Preview.Text;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.Cleanup;
using Alexandria.Services.Storage.Directories;
using Alexandria.Services.Storage.Promotions;
using Alexandria.Services.User;
using Alexandria.Services.User.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IArchivePreviewService, ArchivePreviewService>();
        services.AddScoped<ITextPreviewService, TextPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IPreviewService, PreviewService>();
        services.AddScoped<IFileTagService, FileTagService>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddHttpClient<MetricsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();

        // Background Workers
        services.AddHostedService<PromotionQueueWorker>();
        services.AddHostedService<PromotionScannerWorker>();
        services.AddHostedService<TempCleanupWorker>();
        services.AddHostedService<OrphanedCleanupWorker>();

        services.AddMemoryCache();

        services.AddHttpClient("worker-health")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddResourceMonitoring();
        return services;
    }
}