using RabbitMQ.Client;

namespace Alexandria.Workers.MediaTranspilation.Extensions;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = configuration.GetValue("RabbitMQ:Port", 5672),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
        };

        services.AddSingleton<IConnectionFactory>(factory);

        var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        services.AddSingleton(connection);


        return services;
    }
}