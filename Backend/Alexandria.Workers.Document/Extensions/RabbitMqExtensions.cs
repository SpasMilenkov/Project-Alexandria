using Alexandria.Infrastructure.Messaging;
using Alexandria.Workers.Document.Handlers;

namespace Alexandria.Workers.Document.Extensions;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // The connection is established in the background, so the worker boots without a broker.
        services.AddRabbitMqConnection(configuration);

        services.AddScoped<IPreviewGenerationHandler, PreviewGenerationHandler>();

        return services;
    }
}
