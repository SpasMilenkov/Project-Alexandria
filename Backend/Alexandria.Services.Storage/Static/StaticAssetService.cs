using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Static;

/// <summary>
/// User-facing static bytes (background images, playlist covers).
/// Mechanical extraction from <c>S3Service</c>; behavior preserved byte-for-byte, bugs included.
/// </summary>
public class StaticAssetService(
    IAmazonS3 s3,
    [FromKeyedServices("public")] IAmazonS3 publicS3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config)
    : IStaticAssetService
{
    private static readonly HashSet<string> AllowedCoverTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    public async Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.ImagesBucket,
            Key = $"{objectKey}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTPS
        };

        return await publicS3.GetPreSignedURLAsync(request);
    }

    public async Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        await s3.DeleteObjectAsync(config.Value.ImagesBucket, $"{objectKey}", ct);
    }

    public async Task<string> GenerateBackgroundImageGetUrl(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.ImagesBucket,
            Key = $"{objectKey}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTPS
        };
        return await publicS3.GetPreSignedURLAsync(request);
    }

    public async Task<string> GetPlaylistCoverUploadUrlAsync(Guid playlistId, Guid userId, string contentType,
        CancellationToken ct = default)
    {
        if (!AllowedCoverTypes.Contains(contentType))
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed for covers.");

        if (!await unitOfWork.Playlists.ExistsAsync(p => p.Id == playlistId && p.OwnerId == userId, ct))
            throw new PlaylistNotFoundException(playlistId);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.PreviewBucket,
            Key = $"covers/{playlistId}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)),
            Protocol = Protocol.HTTPS,
            ContentType = contentType,
        };

        return await publicS3.GetPreSignedURLAsync(request);
    }
}