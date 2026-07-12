using Alexandria.Common.Services;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Streaming.Lyrics;
using Alexandria.Workers.MediaMetadata.Handlers;

namespace Alexandria.Workers.MediaMetadata.Extensions;

public static class WorkerExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();
        services.AddRabbitMqAsync(configuration);

        if (!configuration.GetValue<bool>("Features:Lyrics")) return services;

        services.AddHttpClient<LrcLibPublicProvider>();
        services.AddSingleton<ITrackLyricsProvider>(sp => sp.GetRequiredService<LrcLibPublicProvider>());
        services.AddSingleton<CompositeLyricsProvider>();
        services.AddScoped<LyricsHandler>();
        services.AddHostedService<LyricsWorker>();

        return services;
    }
}