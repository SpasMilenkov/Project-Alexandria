using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class ApiExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddFastEndpoints().SwaggerDocument();
        services.AddHealthChecks();
        services.AddCors(c =>
        {
            c.AddPolicy("AllowOrigin",
                options => options.WithOrigins(
                        "https://unitrack.io:8080",
                        "http://localhost:3000",
                        "http://localhost:5173")
                    .AllowCredentials()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
            );
        });

        return services;
    }

    public static void ConfigureKestrelMaxRequestSize(this ConfigureWebHostBuilder webHost)
    {
        webHost.ConfigureKestrel(o =>
        {
            o.Limits.MaxRequestBodySize = 107374182400; // 100 GB
        });
    }
}