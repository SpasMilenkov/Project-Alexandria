using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Alexandria.Infrastructure;

public static class TelemetryExtensions
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "alexandria-api",
                serviceVersion: "1.0.0")) // optional
            .WithMetrics(builder => builder
                .AddAspNetCoreInstrumentation() // http_server_* request duration, active requests
                .AddHttpClientInstrumentation() // outbound HTTP calls (to garage, rabbitmq clients if HTTP-based)
                .AddRuntimeInstrumentation() // GC, thread pool, exceptions, JIT
                .AddProcessInstrumentation()
                .AddPrometheusExporter());

        services.Configure<HealthCheckPublisherOptions>(options => { options.Period = TimeSpan.FromMinutes(1); });

        return services;
    }
}