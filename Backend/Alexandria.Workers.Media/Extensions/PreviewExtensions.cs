using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Images;
using Alexandria.Services.Preview.Media;
using Alexandria.Workers.Media.Handlers;

namespace Alexandria.Workers.Media.Extensions;

public static class PreviewExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();
        services.AddRabbitMqAsync(configuration);

        services.AddScoped<IMediaPreviewService, MediaPreviewService>();
        services.Configure<ImagePreviewConfig>(configuration.GetSection("ImagePreview"));
        services.AddSingleton<MagickCliDecoder>();
        services.AddScoped<IImagePreviewService, ImagePreviewService>();
        services.AddHostedService<ImagePreviewRuntimeCheck>();
        services.AddScoped<ImagePreviewGenerationHandler>();
        services.AddScoped<MediaPreviewGenerationHandler>();

        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.MediaPreviews,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));
        return services;
    }
}