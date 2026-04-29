using Alexandria.Common.Config;
using Alexandria.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Alexandria.Infrastructure;

public static class HealthMonitoringExtension
{
    public static IServiceCollection AddHealthMonitoring(
        this IServiceCollection services, IConfiguration config)
    {
        var hcConfig = config.GetSection("HealthChecks");
        var builder = services.AddHealthChecks();

        if (hcConfig.GetValue<bool>("Database:Enabled"))
            builder.AddCheck<PostgresHealthCheck>("postgres", tags: ["infrastructure"]);

        if (hcConfig.GetValue<bool>("Storage:Enabled"))
            builder.AddCheck<S3HealthCheck>("storage", tags: ["infrastructure"]);

        if (hcConfig.GetValue<bool>("MessageBroker:Enabled"))
            builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["infrastructure"]);

        // Workers: dynamic, config-driven, no hardcoding
        var workers = hcConfig.GetSection("Workers").Get<List<WorkerConfig>>() ?? [];
        foreach (var worker in workers.Where(w => w.Enabled))
        {
            var url = worker.Url;
            var name = worker.Name;

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Add(new HealthCheckRegistration(
                    name: $"worker-{name.ToLower()}",
                    factory: sp => new WorkerHealthCheck(
                        sp.GetRequiredService<IHttpClientFactory>(), url, name),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["worker"]
                ));
            });
        }

        return services;
    }
}