using Common;
using Common.Queues;
using Common.Repositories;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService.Archives;
using PreviewService.Text;
using Repositories;
using Storage;
using Storage.Cleanup;
using Storage.Directories;
using Storage.Promotions;
using User.Service;
using User.Service.Settings;

namespace Infrastructure;

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
        services.AddScoped<IPreviewService, PreviewService.PreviewService>();
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

        services.AddHttpClient("worker-health")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddResourceMonitoring();
        return services;
    }
}
