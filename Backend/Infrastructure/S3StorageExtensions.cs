using Amazon.S3;
using Common.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class S3StorageExtensions
{
    /// <summary>
    /// Adds S3 storage services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddS3Storage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<S3Config>(configuration.GetSection("S3Storage"));

        // Factory
        services.AddSingleton<IS3ClientFactory, S3ClientFactory>();

        // S3 client (created via factory)
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var factory = sp.GetRequiredService<IS3ClientFactory>();
            return factory.CreateClient();
        });


        return services;
    }
}