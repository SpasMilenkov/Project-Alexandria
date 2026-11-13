using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Static;

/// <summary>
/// User-facing static bytes (background images, playlist covers, profile pictures).
/// Mechanical extraction from <c>S3Service</c>; behavior preserved byte-for-byte, bugs included.
/// </summary>
public class StaticAssetService(
    IAmazonS3 s3,
    [FromKeyedServices("public")] IAmazonS3 publicS3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config)
    : IStaticAssetService
{
    internal const long MaxImageBytes = 4 * 1024 * 1024;

    // The browser crops and re-encodes still avatars to a small square before upload, so anything
    // near this cap did not come from the app.
    internal const long MaxAvatarBytes = 1024 * 1024;

    // GIFs upload untouched to keep their animation, so they get more room but a hard size limit:
    // every page that lists users decodes them.
    internal const long MaxGifAvatarBytes = 4 * 1024 * 1024;
    internal const int MaxGifAvatarDimension = 1024;

    private const int DeleteBatchSize = 1000;

    private static readonly HashSet<string> AvatarContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/gif"];

    public async Task<string> UploadBackgroundImageAsync(
        Stream content,
        Guid userId,
        CancellationToken ct = default)
    {
        var bytes = await ReadCappedAsync(content, MaxImageBytes, "Background image", ct);

        var contentType = DetectImageContentType(bytes)
            ?? throw new InvalidOperationException("Upload content is not a supported image format.");

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

    internal static string? DetectImageContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38 &&
            (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    public async Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default)
    {
        await s3.DeleteObjectAsync(config.Value.ImagesBucket, $"{objectKey}", ct);
    }


    public async Task<string> UploadPlaylistCoverAsync(
        Stream content,
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!await unitOfWork.Playlists.ExistsAsync(p => p.Id == playlistId && p.OwnerId == userId, ct))
            throw new PlaylistNotFoundException(playlistId);

        var bytes = await ReadCappedAsync(content, MaxImageBytes, "cover image", ct);

        var contentType = DetectImageContentType(bytes)
            ?? throw new InvalidOperationException("Upload content is not a supported image format.");

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

    public async Task<Guid> UploadAvatarAsync(Stream content, Guid userId, CancellationToken ct = default)
    {
        var bytes = await ReadCappedAsync(content, MaxGifAvatarBytes, "Profile picture", ct);

        var contentType = DetectImageContentType(bytes);
        if (contentType is null || !AvatarContentTypes.Contains(contentType))
            throw new InvalidOperationException("Profile pictures must be JPEG, PNG, WebP or GIF images.");

        if (contentType == "image/gif")
            EnsureGifFits(bytes);
        else if (bytes.Length > MaxAvatarBytes)
            throw new InvalidOperationException($"Profile picture exceeds the {MaxAvatarBytes} byte limit.");

        var avatarId = Guid.NewGuid();

        using var buffered = new MemoryStream(bytes, writable: false);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.Value.ImagesBucket,
            Key = AvatarPaths.Key(userId, avatarId),
            InputStream = buffered,
            ContentType = contentType,
            DisableDefaultChecksumValidation = true,
        }, ct);

        return avatarId;
    }

    public async Task<int> DeleteAvatarsExceptAsync(Guid userId, Guid? keepAvatarId, CancellationToken ct = default)
    {
        string? keepKey = null;
        if (keepAvatarId is not null)
            keepKey = AvatarPaths.Key(userId, keepAvatarId.Value);

        var listRequest = new ListObjectsV2Request
        {
            BucketName = config.Value.ImagesBucket,
            Prefix = AvatarPaths.OwnerPrefix(userId),
        };

        var staleKeys = new List<string>();

        do
        {
            var response = await s3.ListObjectsV2Async(listRequest, ct);
            if (response.S3Objects is null)
                break;

            staleKeys.AddRange(response.S3Objects.Select(o => o.Key).Where(key => key != keepKey));
            listRequest.ContinuationToken = response.NextContinuationToken;
        } while (listRequest.ContinuationToken is not null);

        foreach (var batch in staleKeys.Chunk(DeleteBatchSize))
        {
            await s3.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = config.Value.ImagesBucket,
                Objects = batch.Select(key => new KeyVersion { Key = key }).ToList(),
            }, ct);
        }

        return staleKeys.Count;
    }

    // The logical screen size sits right after the 6-byte signature, little-endian, and bounds
    // every frame, so it is enough to keep oversized animations out.
    internal static void EnsureGifFits(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 10)
            throw new InvalidOperationException("Profile picture is not a complete GIF.");

        var width = bytes[6] | (bytes[7] << 8);
        var height = bytes[8] | (bytes[9] << 8);

        if (width > MaxGifAvatarDimension || height > MaxGifAvatarDimension)
            throw new InvalidOperationException(
                $"Animated profile pictures can be at most {MaxGifAvatarDimension}x{MaxGifAvatarDimension} pixels.");
    }

    internal static async Task<byte[]> ReadCappedAsync(
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