using Alexandria.Infrastructure.Messaging;

namespace Alexandria.Workers.MediaMetadata.Extensions;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // The connection is established in the background, so the worker boots without a broker.
        services.AddRabbitMqConnection(configuration);

        return services;
    }
}
