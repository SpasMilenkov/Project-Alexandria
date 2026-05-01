using Alexandria.Infrastructure.Converters;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}