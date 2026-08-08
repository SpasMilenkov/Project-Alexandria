using Alexandria.Common.Services;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Services;
using Alexandria.Workers.MediaMetadata.Workers;

namespace Alexandria.Workers.MediaMetadata.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();

        services.AddScoped<IStorageService, S3Service>();

        services.AddAutoTagging(configuration);

        // The channel is always available so ResultsConsumerWorker can inject it, but it is
        // only drained while the feature is enabled (the drainer is registered below).
        services.AddSingleton<AutoTagQueueService>();
        services.AddSingleton<IAutoTagQueue>(sp =>
            sp.GetRequiredService<AutoTagQueueService>());

        if (configuration.GetValue<bool>("Features:Autotagging"))
        {
            services.AddScoped<IAutoTagDerivationService, AutoTagDerivationService>();
            services.AddScoped<IAutoTagSyncService, AutoTagSyncService>();
            services.AddHostedService<AutoTagSyncWorker>();
            services.AddHostedService<AutoTagSweepWorker>();
        }

        services.Configure<EssentiaConfig>(configuration.GetSection("Essentia"));
        services.Configure<RabbitMqConsumerConfig>(configuration.GetSection("RabbitMQ:Consumer"));

        services.AddSingleton<AccumulatorBuffer>();

        services.AddScoped<IStagingService, StagingService>();

        return services;
    }
}