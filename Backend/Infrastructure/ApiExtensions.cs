using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure.Converters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class ApiExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
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
        //TODO: Move this to the config
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
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new BigIntegerJsonConverter());
        });

        return services;
    }

    //TODO: Confirm I don't do any more file streaming via the API and remove this.
    public static void ConfigureKestrelMaxRequestSize(this ConfigureWebHostBuilder webHost)
    {
        webHost.ConfigureKestrel(o =>
        {
            o.Limits.MaxRequestBodySize = 107374182400; // 100 GB
        });
    }
}