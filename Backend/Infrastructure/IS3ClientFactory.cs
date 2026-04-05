using Amazon.S3;
using Common.Config;

namespace Infrastructure;

/// <summary>
/// Factory for creating provider-aware S3 clients
/// </summary>
public interface IS3ClientFactory
{
    /// <summary>
    /// Creates an S3 client configured for the current provider
    /// </summary>
    IAmazonS3 CreateClient();

    /// <summary>
    /// Creates an S3 client for the current provider to be used for presigned URLs
    /// </summary>
    IAmazonS3 CreatePublicClient();

    /// <summary>
    /// Gets the current provider type
    /// </summary>
    S3Provider GetProvider();
}
