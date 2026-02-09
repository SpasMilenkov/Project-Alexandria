using Common;
using Common.Queues;
using Common.Repositories;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService.Documents;
using Repositories;
using Storage;
using Storage.Directories;
using Storage.Promotions;

namespace Infrastructure.DocumentWorker;

public static class ServiceExtensions
{
    public static IServiceCollection AddSWorkerServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });


        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IPdfPreviewService, PdfPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();
        return services;
    }
}