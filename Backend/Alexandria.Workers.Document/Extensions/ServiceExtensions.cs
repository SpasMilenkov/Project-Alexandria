using Alexandria.Infrastructure.Workers;
using Alexandria.Services.Preview.Documents;

namespace Alexandria.Workers.Document.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddCoreWorkerServices();

        services.AddScoped<IPdfPreviewService, PdfPreviewService>();

        return services;
    }
}