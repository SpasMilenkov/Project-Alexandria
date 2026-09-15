using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Streaming;
using Alexandria.Workers.Playlist.Config;
using Alexandria.Workers.Playlist.Workers;

namespace Alexandria.Workers.Playlist.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();
        services.AddRabbitMqAsync(configuration);

        services.Configure<PlaylistConsumerConfig>(configuration.GetSection("RabbitMQ:Consumer"));

        services.AddScoped<IAutoPlaylistGroupingService, AutoPlaylistGroupingService>();
        services.AddScoped<IPlaylistJobResolver, PlaylistJobResolver>();
        services.AddScoped<IAutoPlaylistSyncService, AutoPlaylistSyncService>();
        services.AddScoped<IAutoPlaylistSweepService, AutoPlaylistSweepService>();
        services.AddHostedService<PlaylistSyncWorker>();
        services.AddHostedService<PlaylistSweepWorker>();
        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.Playlist,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));

        return services;
    }
}