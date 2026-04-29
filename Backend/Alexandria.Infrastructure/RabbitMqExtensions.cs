using Alexandria.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new ConnectionFactory
            {
                HostName = config["RabbitMQ:HostName"] ?? "localhost",
                Port = config.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = config["RabbitMQ:UserName"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest",
                VirtualHost = config["RabbitMQ:VirtualHost"] ?? "/",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };
        });

        services.AddSingleton<IConnection>(sp =>
            sp.GetRequiredService<IConnectionFactory>()
                .CreateConnectionAsync()
                .GetAwaiter()
                .GetResult());

        services.AddSingleton<IChannelPool>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new ChannelPool(
                sp.GetRequiredService<IConnection>(),
                maxSize: config.GetValue<int>("RabbitMQ:ChannelPoolSize", 10)
            );
        });

        services.AddSingleton<IPublisherService, PublisherService>();
        return services;
    }
}