using Common;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService;
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

        
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IImagePreviewService, ImagePreviewService>();
        services.AddScoped<IPreviewService, PreviewService.PreviewService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}