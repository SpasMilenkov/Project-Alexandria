using Alexandria.Common.Services;
using Alexandria.Common.Config;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Services.Storage.Policies;
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
        services.Configure<ProcessingCapabilities>(configuration.GetSection(ProcessingCapabilities.SectionName));

        services.AddScoped<IStorageService, S3Service>();

        services.AddAutoTagging(configuration);

        // The channel is always available so ResultsConsumerWorker can inject it, but it is
        // only drained while the feature is enabled (the drainer is registered below).
        services.AddSingleton<AutoTagQueueService>();
        services.AddSingleton<IAutoTagQueue>(sp =>
            sp.GetRequiredService<AutoTagQueueService>());

        var audioAnalysisEnabled = configuration.GetValue("Features:AudioAnalysis", true);
        if (configuration.GetValue<bool>("Features:Autotagging") && audioAnalysisEnabled)
        {
            services.AddScoped<IAutoTagDerivationService, AutoTagDerivationService>();
            services.AddScoped<IAutoTagSyncService, AutoTagSyncService>();
            services.AddScoped<IEnrichmentBackfillService, EnrichmentBackfillService>();
            services.AddHostedService<AutoTagSyncWorker>();
            services.AddHostedService<AutoTagSweepWorker>();
            services.AddHostedService<EnrichmentBackfillWorker>();
        }

        services.Configure<EssentiaConfig>(configuration.GetSection("Essentia"));
        services.Configure<RabbitMqConsumerConfig>(configuration.GetSection("RabbitMQ:Consumer"));

        services.AddSingleton<AccumulatorBuffer>();

        services.AddScoped<IStagingService, StagingService>();

        if (audioAnalysisEnabled)
        {
            services.AddHostedService<TriggerConsumerWorker>();
            services.AddHostedService<AccumulatorWorker>();
            services.AddHostedService<ResultsConsumerWorker>();
            services.AddHostedService<TimeoutSweepWorker>();
        }

        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.MediaMetadata,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));

        return services;
    }
}
