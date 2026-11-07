using System.Globalization;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Static;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Previews;

public partial class PreviewArtifactService
{
    public async Task<DateTime> SetCustomCoverAsync(
        Guid fileId, Guid versionId, Guid userId, Stream content, CancellationToken ct = default)
    {
        var version = await GetOwnedVersionAsync(fileId, versionId, userId, ct);

        if (!version.File.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only audio files can have a custom cover.");

        if (version.IsEncrypted)
            throw new InvalidOperationException("Encrypted versions cannot have a custom cover.");

        if (!version.ContentObject.IsPromoted)
            throw new InvalidOperationException("This version is still being processed.");

        var bytes = await StaticAssetService.ReadCappedAsync(
            content, StaticAssetService.MaxImageBytes, "Cover image", ct);

        var contentType = StaticAssetService.DetectImageContentType(bytes)
            ?? throw new InvalidOperationException("Upload content is not a supported image format.");

        // thumbnails/{hash} is shared by every version with the same content, so a cover
        // scoped to one version needs its own object. The key rotates per upload because
        // nginx caches by object key.
        var bucket = config.Value.PreviewBucket;
        var objectKey = $"track-covers/{versionId}/{Guid.NewGuid()}";
        string? previousKey = null;
        DateTime updatedAt;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            using var buffered = new MemoryStream(bytes, writable: false);

            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = objectKey,
                InputStream = buffered,
                ContentType = contentType,
                DisableDefaultChecksumValidation = true,
            }, ct);

            var existing = await unitOfWork.Previews.FirstOrDefaultAsync(
                p => p.VersionId == versionId && p.Kind == PreviewKind.Thumbnail, ct);

            if (existing is null)
            {
                var row = new Preview
                {
                    Id = Guid.NewGuid(),
                    MimeType = contentType,
                    Size = bytes.Length,
                    Kind = PreviewKind.Thumbnail,
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow,
                    VersionId = versionId,
                    ObjectKey = objectKey,
                    IsManual = true,
                };

                await unitOfWork.Previews.CreateAsync(row, ct);
                updatedAt = row.UpdatedAt.Value;
            }
            else
            {
                // A generated thumbnail object stays behind on purpose: its key may be shared
                // with other versions, and it gets rewritten by the next generation run anyway.
                previousKey = existing.IsManual ? existing.ObjectKey : null;

                existing.MimeType = contentType;
                existing.Size = bytes.Length;
                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.ObjectKey = objectKey;
                existing.IsManual = true;

                await unitOfWork.Previews.UpdateAsync(existing, ct);
                updatedAt = existing.UpdatedAt.Value;
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            LogManualCoverSaveFailed(logger, ex, versionId, objectKey);

            await unitOfWork.RollbackAsync(ct);
            await TryDeleteCoverObjectAsync(objectKey, ct);

            throw;
        }

        if (previousKey is not null)
            await TryDeleteCoverObjectAsync(previousKey, ct);

        LogManualCoverSaved(logger, versionId, objectKey, bytes.Length);

        return updatedAt;
    }

    public async Task RemoveCustomCoverAsync(
        Guid fileId, Guid versionId, Guid userId, CancellationToken ct = default)
    {
        await GetOwnedVersionAsync(fileId, versionId, userId, ct);

        var cover = await GetManualThumbnailAsync(versionId, ct);
        if (cover is null)
            return;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            await DeletePreviewRowAsync(cover, [cover.Id], ct);
            await unitOfWork.CommitAsync(ct);

            LogManualCoverRemoved(logger, versionId);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<string> GetThumbnailContentPathAsync(
        Guid versionId, string contentHash, CancellationToken ct = default)
    {
        var cover = await GetManualThumbnailAsync(versionId, ct);

        return cover?.ObjectKey ?? $"thumbnails/{contentHash}";
    }

    public async Task<string?> GetCoverStampAsync(Guid versionId, CancellationToken ct = default)
    {
        var cover = await GetManualThumbnailAsync(versionId, ct);

        return cover is null ? null : CoverStamp(cover);
    }

    private static string CoverStamp(Preview preview) =>
        (preview.UpdatedAt ?? preview.CreatedAt).Ticks.ToString(CultureInfo.InvariantCulture);

    private Task<Preview?> GetManualThumbnailAsync(Guid versionId, CancellationToken ct) =>
        unitOfWork.Previews.FirstOrDefaultAsync(
            p => p.VersionId == versionId && p.Kind == PreviewKind.Thumbnail && p.IsManual, ct);

    private async Task<FileVersion> GetOwnedVersionAsync(
        Guid fileId, Guid versionId, Guid userId, CancellationToken ct)
    {
        var version = await unitOfWork.FileVersions.GetByIdAsync(versionId, ct);

        if (version is null
            || version.DeletedAt is not null
            || version.FileId != fileId
            || version.File.OwnerId != userId
            || version.File.DeletedAt is not null)
        {
            throw new KeyNotFoundException($"Version {versionId} of file {fileId} not found.");
        }

        return version;
    }

    // An orphaned object only wastes a little space, so a failure here must not fail a cover
    // change that has already been committed or is already being reported as failed.
    private async Task TryDeleteCoverObjectAsync(string key, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(config.Value.PreviewBucket, key, ct);
        }
        catch (Exception ex)
        {
            LogManualCoverObjectOrphaned(logger, ex, key);
        }
    }
}
