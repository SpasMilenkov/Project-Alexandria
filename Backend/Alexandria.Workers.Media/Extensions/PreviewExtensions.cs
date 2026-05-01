using Alexandria.Common.Services;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Media;
using Alexandria.Workers.Media.Handlers;

namespace Alexandria.Workers.Media.Extensions;

public static class PreviewExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddCoreWorkerServices();

        services.AddScoped<IMediaPreviewService, MediaPreviewService>();
        services.AddScoped<IImagePreviewService, ImagePreviewService>();
        services.AddScoped<ImagePreviewGenerationHandler>();
        services.AddScoped<MediaPreviewGenerationHandler>();

        return services;
    }
}