using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Streaming;
using Alexandria.Services.Streaming.Backfill;
using Alexandria.Services.Streaming.VideoEncoding;
using Alexandria.Workers.MediaTranspilation.Config;
using Alexandria.Workers.MediaTranspilation.Handlers;

namespace Alexandria.Workers.MediaTranspilation.Extensions;

public static class WorkerExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();
        services.Configure<ProcessingCapabilities>(configuration.GetSection(ProcessingCapabilities.SectionName));

        services.Configure<TranspilationConfig>(
            configuration.GetSection("Transpilation"));
        services.Configure<VideoEncodingConfig>(
            configuration.GetSection("VideoEncoding"));

        services.AddSingleton<IGpuEncoderProbe, VaapiEncoderProbe>();
        services.AddSingleton<IVideoEncoderSelector, VideoEncoderSelector>();
        services.AddHostedService<VideoEncoderStartupCheck>();

        services.AddScoped<ITranspilationJobService, TranspilationJobService>();
        services.AddScoped<ITranscodeBackfillService, TranscodeBackfillService>();
        services.AddScoped<IStreamingRepresentationService, StreamingRepresentationService>();
        services.AddScoped<IVideoTranspilationService, VideoTranspilationService>();
        services.AddScoped<IAudioTranspilationService, AudioTranspilationService>();
        services.AddRabbitMqAsync(configuration, ServiceType.Transpilation);
        services.AddScoped<TranspilationJobHandler>();

        // Monitoring
        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.Transpilation,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));
        return services;
    }
}