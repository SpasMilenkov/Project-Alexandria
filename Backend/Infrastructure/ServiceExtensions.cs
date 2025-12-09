using Common;
using Common.Repositories;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using Models;
using PreviewService;
using PreviewService.Archives;
using PreviewService.Text;
using Repositories;
using Storage;

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

        
        services.AddScoped<IStorageService, S3StorageService>();
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}