using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using MinioConfig = Common.Config.MinioConfig;

namespace Infrastructure;

public static class MinioExtensions
{
    public static IServiceCollection AddMinio(this IServiceCollection services, IConfiguration config)
    {
        var minioConfig = config.GetSection("Minio").Get<MinioConfig>();
        if (minioConfig is null) throw new InvalidOperationException("Minio configuration is missing");

        services.Configure<MinioConfig>(config.GetSection("Minio"));

        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(minioConfig.Endpoint)
                .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey);

            return client.Build();
        });

        return services;
    }

    public static async Task SetupMinioBucketAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var minioConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection("Minio").Get<MinioConfig>();

        if (minioConfig is null) return;

        var minioClient = scope.ServiceProvider.GetRequiredService<IMinioClient>();

        try
        {
            // Set up upload bucket on startup
            bool uploadsBucketExists = await minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(minioConfig.UploadBucket));

            if (!uploadsBucketExists)
            {
                await minioClient.MakeBucketAsync(new MakeBucketArgs()
                    .WithBucket(minioConfig.UploadBucket));
            }
            
            await minioClient.SetVersioningAsync(new SetVersioningArgs()
                .WithBucket(minioConfig.PreviewBucket)
                .WithVersioningEnabled());
            bool previewBucketExists = await minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(minioConfig.PreviewBucket));

            if (!previewBucketExists)
            {
                await minioClient.MakeBucketAsync(new MakeBucketArgs()
                    .WithBucket(minioConfig.PreviewBucket));
            }

            

            Console.WriteLine($"MinIO bucket '{minioConfig.UploadBucket}' is ready with versioning enabled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up MinIO bucket: {ex.Message}");
        }
    }
}