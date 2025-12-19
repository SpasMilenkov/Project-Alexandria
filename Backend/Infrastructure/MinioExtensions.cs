using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Common.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class MinioExtensions
{
    public static IServiceCollection AddMinio(this IServiceCollection services, IConfiguration config)
    {
        var minioConfig = config.GetSection("S3Storage").Get<S3Config>();
        if (minioConfig is null) throw new InvalidOperationException("S3 storage configuration is missing");

        services.Configure<S3Config>(config.GetSection("S3Storage"));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var storageConfiguration = sp.GetRequiredService<IOptions<S3Config>>().Value;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = storageConfiguration.Endpoint,
                ForcePathStyle = true,
                UseHttp = storageConfiguration.Endpoint != null && !storageConfiguration.Endpoint.StartsWith("https")
            };

            return new AmazonS3Client(storageConfiguration.AccessKey, storageConfiguration.SecretKey, s3Config);
        });

        return services;
    }

    public static async Task SetupS3BucketAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var s3Config = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetSection("Minio").Get<S3Config>();

        if (s3Config is null) return;

        var s3Client = scope.ServiceProvider.GetRequiredService<IAmazonS3>();

        try
        {
            // Set up upload bucket on startup
            var uploadsBucketExists = await BucketExistsAsync(s3Client, s3Config.UploadBucket);

            if (!uploadsBucketExists)
                await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = s3Config.UploadBucket
                });

            // Enable versioning on upload bucket
            await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
            {
                BucketName = s3Config.UploadBucket,
                VersioningConfig = new S3BucketVersioningConfig
                {
                    Status = VersionStatus.Enabled
                }
            });

            // Set up preview bucket
            var previewBucketExists = await BucketExistsAsync(s3Client, s3Config.PreviewBucket);

            if (!previewBucketExists)
                await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = s3Config.PreviewBucket
                });

            // Enable versioning on preview bucket
            await s3Client.PutBucketVersioningAsync(new PutBucketVersioningRequest
            {
                BucketName = s3Config.PreviewBucket,
                VersioningConfig = new S3BucketVersioningConfig
                {
                    Status = VersionStatus.Enabled
                }
            });

            Console.WriteLine(
                $"S3 buckets '{s3Config.UploadBucket}' and '{s3Config.PreviewBucket}' are ready with versioning enabled");
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error setting up S3 buckets: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up S3 buckets: {ex.Message}");
        }
    }

    private static async Task<bool> BucketExistsAsync(IAmazonS3 s3Client, string bucketName)
    {
        try
        {
            await s3Client.GetBucketLocationAsync(bucketName);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}