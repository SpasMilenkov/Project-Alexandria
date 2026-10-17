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
    private static readonly HashSet<string> AllowedImageTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    private const long MaxImageBytes = 4 * 1024 * 1024;

    public async Task<string> UploadBackgroundImageAsync(
        Stream content,
        string contentType,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!AllowedImageTypes.Contains(contentType))
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed for backgrounds.");

        var bytes = await ReadCappedAsync(content, MaxImageBytes, "Background image", ct);

        if (!MagicMatchesContentType(bytes, contentType))
            throw new InvalidOperationException($"Upload content does not match the declared type '{contentType}'.");

        var objectKey = $"background_images/{userId}/{Guid.NewGuid()}";

        using var buffered = new MemoryStream(bytes, writable: false);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.Value.ImagesBucket,
            Key = objectKey,
            InputStream = buffered,
            ContentType = contentType,
            DisableDefaultChecksumValidation = true,
        }, ct);

        return objectKey;
    }

    private static bool MagicMatchesContentType(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4)
            return false;

        return contentType switch
        {
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E &&
                           bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A &&
                           bytes[7] == 0x0A,
            "image/gif" => bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
                           bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61,
            "image/webp" => bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
                            bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 &&
                            bytes[11] == 0x50,
            _ => false
        };
    }

    public async Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        await s3.DeleteObjectAsync(config.Value.ImagesBucket, $"{objectKey}", ct);
    }


    public async Task<string> UploadPlaylistCoverAsync(
        Stream content,
        string contentType,
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!AllowedImageTypes.Contains(contentType))
            throw new InvalidOperationException($"Content type '{contentType}' is not allowed for covers.");

        if (!await unitOfWork.Playlists.ExistsAsync(p => p.Id == playlistId && p.OwnerId == userId, ct))
            throw new PlaylistNotFoundException(playlistId);

        var bytes = await ReadCappedAsync(content, MaxImageBytes, "cover image", ct);

        if (!MagicMatchesContentType(bytes, contentType))
            throw new InvalidOperationException($"Upload content does not match the declared type '{contentType}'.");

        var objectKey = $"covers/{playlistId}";

        using var buffered = new MemoryStream(bytes, writable: false);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.Value.PreviewBucket,
            Key = objectKey,
            InputStream = buffered,
            ContentType = contentType,
            DisableDefaultChecksumValidation = true,
        }, ct);

        return objectKey;
    }

    private static async Task<byte[]> ReadCappedAsync(
        Stream content, long cap, string what, CancellationToken ct)
    {
        using var buffered = new MemoryStream();

        var remaining = cap + 1;
        var buffer = new byte[81920];

        int read;
        while (remaining > 0 &&
               (read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct)) != 0)
        {
            await buffered.WriteAsync(buffer.AsMemory(0, read), ct);
            remaining -= read;
        }

        return remaining == 0
            ? throw new InvalidOperationException($"{what} exceeds the {cap} byte limit.")
            : buffered.ToArray();
    }
}