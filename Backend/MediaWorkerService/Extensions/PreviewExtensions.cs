using Common;
using Common.Repositories;
using Common.Services;
using Data;
using PreviewService.Media;
using Repositories;
using Storage;
using Storage.Directories;

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

        
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IMediaPreviewService, MediaPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}