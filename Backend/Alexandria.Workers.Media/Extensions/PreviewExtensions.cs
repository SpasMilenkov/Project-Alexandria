using Alexandria.Common;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data;
using Alexandria.Repositories;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Media;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.Directories;
using Alexandria.Services.Storage.Promotions;
using Alexandria.Workers.Media.Handlers;

namespace Alexandria.Workers.Media.Extensions;

public static class PreviewExtensions
{
    public static IServiceCollection AddPreviewWorkerServices(this IServiceCollection services)
    {
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