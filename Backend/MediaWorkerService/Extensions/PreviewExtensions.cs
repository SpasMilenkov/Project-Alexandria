using Common;
using Common.Queues;
using Common.Repositories;
using Common.Services;
using Data;
using MediaWorkerService.Handlers;
using PreviewService;
using PreviewService.Media;
using Repositories;
using Storage;
using Storage.Directories;
using Storage.Promotions;

namespace MediaWorkerService.Extensions;

public static class PreviewExtensions
{
    public static IServiceCollection AddPreviewWorkerServices(this IServiceCollection services)
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
        services.AddScoped<IMediaPreviewService, MediaPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();
        services.AddScoped<ImagePreviewGenerationHandler>();
        services.AddScoped<MediaPreviewGenerationHandler>();
        services.AddScoped<IImagePreviewService, ImagePreviewService>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();
        return services;
    }
}
