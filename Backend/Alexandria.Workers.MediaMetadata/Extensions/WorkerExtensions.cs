using System.Threading.RateLimiting;
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

        services.AddSingleton<RateLimiter>(_ => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 2,
            Window = TimeSpan.FromSeconds(10),
            SegmentsPerWindow = 2,
            QueueLimit = 25,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));

        services.AddTransient<RateLimitingHandler>();

        services.AddHttpClient<LrcLibPublicProvider>()
            .AddHttpMessageHandler<RateLimitingHandler>();
        services.AddSingleton<ITrackLyricsProvider>(sp => sp.GetRequiredService<LrcLibPublicProvider>());
        services.AddSingleton<CompositeLyricsProvider>();
        services.AddScoped<LyricsHandler>();
        services.AddHostedService<LyricsWorker>();

        return services;
    }
}