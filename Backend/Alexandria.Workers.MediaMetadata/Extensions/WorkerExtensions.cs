using Alexandria.Common.Services;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Streaming;
using Alexandria.Services.Streaming.Lyrics;

namespace AlexandriaW.Workers.MediaMetadata.Extensions;

public static class WorkerExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();

        services.AddHttpClient<LrcLibPublicProvider>();
        services.AddSingleton<ITrackLyricsProvider>(sp => sp.GetRequiredService<LrcLibPublicProvider>());
        services.AddSingleton<CompositeLyricsProvider>();

        services.AddScoped<ITranspilationJobService, TranspilationJobService>();
        services.AddScoped<IStreamingRepresentationService, StreamingRepresentationService>();
        services.AddScoped<IVideoTranspilationService, VideoTranspilationService>();
        services.AddScoped<IAudioTranspilationService, AudioTranspilationService>();
        services.AddRabbitMqAsync(configuration);
        services.AddScoped<IPublisherService, PublisherService>();

        return services;
    }
}