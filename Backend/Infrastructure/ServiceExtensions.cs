using Common;
using Common.Queues;
using Common.Repositories;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService;
using PreviewService.Archives;
using PreviewService.Text;
using Repositories;
using Storage;
using Storage.Cleanup;
using Storage.Directories;
using Storage.Promotions;

namespace Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IArchivePreviewService, ArchivePreviewService>();
        services.AddScoped<ITextPreviewService, TextPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IImagePreviewService, ImagePreviewService>();
        services.AddScoped<IPreviewService, PreviewService.PreviewService>();
        services.AddScoped<IFileTagService, FileTagService>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();

        // Background Workers
        services.AddHostedService<PromotionQueueWorker>();
        services.AddHostedService<PromotionScannerWorker>();
        services.AddHostedService<TempCleanupWorker>();
        services.AddHostedService<OrphanedCleanupWorker>();

        services.AddResourceMonitoring();
        return services;
    }
}