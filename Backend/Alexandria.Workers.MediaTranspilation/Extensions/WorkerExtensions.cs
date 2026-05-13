using Alexandria.Common.Services;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Streaming;
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

        services.Configure<TranspilationConfig>(
            configuration.GetSection("Transpilation"));

        services.AddScoped<ITranspilationJobService, TranspilationJobService>();
        services.AddScoped<IStreamingRepresentationService, StreamingRepresentationService>();
        services.AddScoped<IVideoTranspilationService, VideoTranspilationService>();
        services.AddScoped<IAudioTranspilationService, AudioTranspilationService>();
        services.AddScoped<TranspilationJobHandler>();

        return services;
    }
}