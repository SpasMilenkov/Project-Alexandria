using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Preview.Documents;
using Alexandria.Services.Preview.Documents.Metadata;

namespace Alexandria.Workers.Document.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCoreWorkerServices();
        services.AddRabbitMqAsync(configuration);

        services.AddScoped<IPdfPreviewService, PdfPreviewService>();
        services.AddScoped<IDocumentMetadataService, DocumentMetadataService>();

        services.Configure<DocumentMetadataSweepConfig>(configuration.GetSection("DocumentMetadataSweep"));
        services.AddHostedService<DocumentMetadataSweepWorker>();

        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.DocumentPreviews,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));
        return services;
    }
}