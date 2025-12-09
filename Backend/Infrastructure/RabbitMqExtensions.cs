using Common;
using Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Infrastructure;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register ConnectionFactory as singleton
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = configuration.GetValue<int>("RabbitMQ:Port", 5672),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };

        services.AddSingleton<IConnectionFactory>(factory);

        var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        services.AddSingleton<IConnection>(connection);
    
        // Register channel pool
        services.AddSingleton<IChannelPool>(sp => 
            new ChannelPool(
                sp.GetRequiredService<IConnection>(), 
                maxSize: configuration.GetValue<int>("RabbitMQ:ChannelPoolSize", 10)
            ));

        services.AddSingleton<IPublisherService, PublisherService>();
        return services;
    }
}

