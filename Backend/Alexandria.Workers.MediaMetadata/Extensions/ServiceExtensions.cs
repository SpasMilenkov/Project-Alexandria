using Alexandria.Common.Services;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Storage;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Services;

namespace Alexandria.Workers.MediaMetadata.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();

        services.AddScoped<IStorageService, S3Service>();

        services.Configure<EssentiaConfig>(configuration.GetSection("Essentia"));
        services.Configure<RabbitMqConsumerConfig>(configuration.GetSection("RabbitMQ:Consumer"));

        services.AddSingleton<AccumulatorBuffer>();

        services.AddScoped<IStagingService, StagingService>();

        return services;
    }
}