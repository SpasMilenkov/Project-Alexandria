using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.Messaging;

public static class RabbitMqExtensions
{
    /// <summary>
    /// Registers the single shared broker connection and its background supervisor. Safe to
    /// call more than once, so a host that both consumes and publishes keeps one connection.
    /// </summary>
    public static IServiceCollection AddRabbitMqConnection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IConnectionFactory>(_ => CreateConnectionFactory(configuration));
        services.TryAddSingleton(_ => new MessageBrokerConnection(IsBrokerEnabled(configuration)));
        services.TryAddSingleton<IMessageBrokerStatus>(sp => sp.GetRequiredService<MessageBrokerConnection>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessageBrokerSupervisor>());

        return services;
    }

    /// <summary>
    /// Registers the publisher stack on top of <see cref="AddRabbitMqConnection"/>: the outbox
    /// writer application code enqueues through, and the relay that sends committed messages.
    /// Every host that publishes runs its own relay, and <paramref name="host"/> names it in
    /// the incidents that relay raises.
    /// </summary>
    public static IServiceCollection AddRabbitMqAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceType host)
    {
        services.AddRabbitMqConnection(configuration);

        services.TryAddSingleton<IChannelPool>(sp =>
            new ChannelPool(
                sp.GetRequiredService<MessageBrokerConnection>(),
                TimeSpan.FromSeconds(configuration.GetValue("RabbitMQ:PublishWaitSeconds", 2)),
                maxSize: configuration.GetValue("RabbitMQ:ChannelPoolSize", 10)
            ));

        services.TryAddSingleton<IPublisherService, PublisherService>();
        services.TryAddSingleton<DeliveryRetry>();

        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));
        services.TryAddSingleton(new OutboxHost { Service = host });
        services.TryAddSingleton<OutboxSignal>();
        services.TryAddSingleton<IOutboxSignal>(sp => sp.GetRequiredService<OutboxSignal>());
        services.TryAddScoped<IOutbox, OutboxWriter>();
        services.TryAddScoped<OutboxDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxRelay>());

        return services;
    }

    public static bool IsBrokerEnabled(IConfiguration configuration)
        => configuration.GetValue("RabbitMQ:Enabled", true);

    private static ConnectionFactory CreateConnectionFactory(IConfiguration configuration) => new()
    {
        HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
        Port = configuration.GetValue("RabbitMQ:Port", 5672),
        UserName = configuration["RabbitMQ:UserName"] ?? "guest",
        Password = configuration["RabbitMQ:Password"] ?? "guest",
        VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(60)
    };
}
