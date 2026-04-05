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
        services.Configure<S3Config>(configuration.GetSection("S3Storage"));
        services.AddSingleton<IS3ClientFactory, S3ClientFactory>();

        // Internal client — backend ↔ garage directly
        services.AddKeyedSingleton<IAmazonS3>("internal", (sp, _) =>
            sp.GetRequiredService<IS3ClientFactory>().CreateClient());

        // Public client — presigned URLs signed with the nginx-facing host
        services.AddKeyedSingleton<IAmazonS3>("public", (sp, _) =>
            sp.GetRequiredService<IS3ClientFactory>().CreatePublicClient());

        // Default registration stays as internal so nothing else breaks
        services.AddSingleton<IAmazonS3>(sp =>
            sp.GetRequiredKeyedService<IAmazonS3>("internal"));

        return services;
    }
}
