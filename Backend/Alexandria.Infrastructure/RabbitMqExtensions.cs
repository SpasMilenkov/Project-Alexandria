using System.Diagnostics;
using Alexandria.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = configuration.GetValue("RabbitMQ:Port", 5672),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        });

        // Lazy<T> means the factory runs on first .Value access, not at registration time.
        // PublisherService accesses it only when it actually publishes, well after startup.
        services.AddSingleton<Lazy<Task<IConnection>>>(sp =>
        {
            var factory = sp.GetRequiredService<IConnectionFactory>();
            var logger = sp.GetRequiredService<ILogger<IConnection>>();

            return new Lazy<Task<IConnection>>(() => CreateConnectionWithRetryAsync(factory, logger));
        });

        services.AddSingleton<IChannelPool>(sp =>
            new ChannelPool(
                sp.GetRequiredService<Lazy<Task<IConnection>>>().Value.GetAwaiter().GetResult(),
                maxSize: configuration.GetValue("RabbitMQ:ChannelPoolSize", 10)
            ));

        services.AddSingleton<IPublisherService, PublisherService>();
        return services;
    }

    private static async Task<IConnection> CreateConnectionWithRetryAsync(
        IConnectionFactory factory,
        ILogger logger)
    {
        const int maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var connection = await factory.CreateConnectionAsync();
                logger.LogInformation("RabbitMQ connected on attempt {Attempt}", attempt);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ attempt {Attempt}/{Max} failed, retrying in {Delay}s",
                    attempt, maxAttempts, delay.TotalSeconds);

                if (attempt == maxAttempts)
                    throw new InvalidOperationException($"Could not connect to RabbitMQ after {maxAttempts} attempts.", ex);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30));
            }
        }

        throw new UnreachableException();
    }
}
