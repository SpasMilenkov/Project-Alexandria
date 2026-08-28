using Alexandria.Common.Settings;
using Alexandria.Infrastructure.Converters;
using Alexandria.Services.Monitoring;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Alexandria.Infrastructure;

public static class ApiExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddFastEndpoints()
            .SwaggerDocument(o =>
            {
                o.DocumentSettings = s =>
                {
                    s.DocumentName = "Beta release";
                    s.Title = "Alexandria API";
                    s.Version = "v0";
                };
            });
        services.AddResponseCaching();
        services.AddHealthChecks();

        services.AddSingleton<IHealthCheckPublisher, OperationalEventHealthPublisher>();

        services.Configure<HealthCheckPublisherOptions>(options => { options.Period = TimeSpan.FromMinutes(1); });

        var origins = config
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        if (origins == null || origins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins is not configured");

        services.AddCors(c =>
        {
            c.AddPolicy("AllowOrigin", policy => policy
                .WithOrigins(origins)
                .AllowCredentials()
                .AllowAnyMethod()
                .AllowAnyHeader()
            );
        });
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new BigIntegerJsonConverter());
        });

        services.AddMemoryCache(options => { options.SizeLimit = 3000; });

        services.Configure<EnrichmentMonitoringOptions>(config.GetSection("Monitoring:Enrichment"));

        return services;
    }
}