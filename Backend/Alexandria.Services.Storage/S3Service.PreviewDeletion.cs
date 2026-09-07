using System.Net;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Amazon.S3;

namespace Alexandria.Services.Storage;

public partial class S3Service
{
    internal static string ResolvePreviewKey(Preview preview)
    {
        if (!string.IsNullOrWhiteSpace(preview.ObjectKey))
            return preview.ObjectKey;

        if (preview.Version == null)
            throw new InvalidOperationException($"Preview {preview.Id} has no version for key fallback.");

        var hash = Convert.ToHexStringLower(preview.Version.ContentHash);

        if (preview.Kind == PreviewKind.Thumbnail)
            return $"thumbnails/{hash}";

        return $"previews/{hash}";
    }

    public async Task<long> DeletePreviewAsync(
        Guid previewId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var preview = await unitOfWork.Previews.GetWithFileAsync(previewId, ct)
                      ?? throw new KeyNotFoundException($"Preview {previewId} not found.");

        var ownerId = preview.Version?.File?.OwnerId
                      ?? throw new KeyNotFoundException($"Preview {previewId} has no owning file.");

        if (!isAdmin && ownerId != userId)
            throw new UnauthorizedAccessException("Preview does not belong to the user.");

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await DeletePreviewRowAsync(preview, [preview.Id], ct);
            await unitOfWork.CommitAsync(ct);

            LogPreviewDeleted(logger, previewId, preview.Size);
            return preview.Size;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(int DeletedCount, long FreedBytes)> DeletePreviewsByFileAsync(
        Guid fileId, Guid userId, bool isAdmin, DateTime? createdBefore, CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, ct)
                   ?? throw new KeyNotFoundException($"File {fileId} not found.");

        if (!isAdmin && file.OwnerId != userId)
            throw new UnauthorizedAccessException("File does not belong to the user.");

        var previews = await unitOfWork.Previews.GetByFileAsync(fileId, createdBefore, ct);

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var excludedIds = previews.Select(p => p.Id).ToHashSet();

            foreach (var preview in previews)
                await DeletePreviewRowAsync(preview, excludedIds, ct);

            await unitOfWork.CommitAsync(ct);

            var freedBytes = previews.Sum(p => p.Size);
            LogPreviewsDeletedByFile(logger, fileId, previews.Count, freedBytes);
            return (previews.Count, freedBytes);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Removes one preview row, deleting its S3 object first unless another live preview
    /// still references the same key (versions sharing a content hash share one object).
    /// Job and PreviewJob rows are never touched; previews regenerate from the file.
    /// </summary>
    private async Task DeletePreviewRowAsync(
        Preview preview, HashSet<Guid> excludedIds, CancellationToken ct)
    {
        var bucket = config.Value.PreviewBucket;
        var key = ResolvePreviewKey(preview);

        var shared = await unitOfWork.Previews.ExistsAsync(
            p => !excludedIds.Contains(p.Id) && p.DeletedAt == null && p.ObjectKey == key, ct);

        if (shared)
        {
            LogPreviewObjectShared(logger, preview.Id, key);
        }
        else
        {
            try
            {
                await s3.DeleteObjectAsync(bucket, key, ct);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                LogPreviewObjectNotFound(logger, bucket, key);
            }
        }

        unitOfWork.Previews.Remove(preview);
    }
}