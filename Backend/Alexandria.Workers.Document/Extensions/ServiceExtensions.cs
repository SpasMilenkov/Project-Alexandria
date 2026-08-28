using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Preview.Documents;

namespace Alexandria.Workers.Document.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddCoreWorkerServices();

        services.AddScoped<IPdfPreviewService, PdfPreviewService>();

        services.AddHostedService(sp => new JobFailureThresholdChecker(
            sp.GetRequiredService<IJobOutcomeTracker>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            ServiceType.DocumentPreviews,
            sp.GetRequiredService<ILogger<JobFailureThresholdChecker>>()));
        return services;
    }
}