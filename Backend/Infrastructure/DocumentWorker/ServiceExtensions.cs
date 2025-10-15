using Common;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService;
using PreviewService.Documents;
using Repositories;
using Storage;

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

        
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddScoped<IPdfPreviewService, PdfPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}