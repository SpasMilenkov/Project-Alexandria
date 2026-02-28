using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Test.Common.TestContainers;
using Xunit;

namespace Test.Common.Fixtures;

public class S3Fixture : IAsyncLifetime
{
    private readonly GarageContainer _garage = new();

    public IAmazonS3 Client { get; private set; } = null!;
    public string Endpoint { get; private set; } = null!;
    public string AccessKey { get; private set; } = null!;
    public string SecretKey { get; private set; } = null!;
    public string Region => _garage.Region;

    public static readonly string[] Buckets =
    [
        "alexandria-files",
        "alexandria-previews",
        "alexandria-temp",
        "alexandria-images"
    ];

    public async ValueTask InitializeAsync()
    {
        await _garage.StartAsync();

        Endpoint = _garage.Endpoint;
        AccessKey = _garage.AccessKey;
        SecretKey = _garage.SecretKey;

        Client = BuildS3Client();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _garage.DisposeAsync();
    }

    /// <summary>
    /// Deletes all objects from all buckets between tests without
    /// recreating the buckets themselves, keeping reset cost low.
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        foreach (var bucket in Buckets)
        {
            var objects = await Client.ListObjectsV2Async(
                new() { BucketName = bucket }, ct);

            if (objects.S3Objects.Count == 0)
                continue;

            await Client.DeleteObjectsAsync(
                new()
                {
                    BucketName = bucket,
                    Objects = objects.S3Objects
                        .Select(o => new KeyVersion { Key = o.Key })
                        .ToList()
                }, ct);
        }
    }

    /// <summary>
    /// Convenience helper for putting raw bytes directly into a bucket,
    /// bypassing the service layer — useful for setting up test state.
    /// </summary>
    public async Task PutObjectAsync(
        string bucket,
        string key,
        byte[] data,
        string contentType = "application/octet-stream",
        CancellationToken ct = default)
    {
        await Client.PutObjectAsync(
            new()
            {
                BucketName = bucket,
                Key = key,
                InputStream = new MemoryStream(data),
                ContentType = contentType,
                DisableDefaultChecksumValidation = true,
            }, ct);
    }

    /// <summary>
    /// Returns true if the object exists, false if it was deleted or never created.
    /// Used to assert cleanup happened correctly after failed uploads.
    /// </summary>
    public async Task<bool> ObjectExistsAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        try
        {
            await Client.GetObjectMetadataAsync(bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private AmazonS3Client BuildS3Client()
    {
        var credentials = new BasicAWSCredentials(AccessKey, SecretKey);

        var config = new AmazonS3Config
        {
            ServiceURL = Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = Region,
        };

        return new AmazonS3Client(credentials, config);
    }
}