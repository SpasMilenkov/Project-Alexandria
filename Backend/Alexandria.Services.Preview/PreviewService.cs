using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Services.Preview;

public class PreviewService(
    IStorageService storageService,
    IPublisherService publisherService,
    IUnitOfWork unitOfWork,
    ITextPreviewService textPreviewService,
    IArchivePreviewService archivePreviewService,
    IMemoryCache memoryCache) : IPreviewService
{
    private static readonly TimeSpan JobInFlightTtl = TimeSpan.FromMinutes(10);

    private static string JobKey(Guid versionId) => $"preview-job:{versionId}";

    // Returns true if this call is the one that should start the job (no job was in flight).
    // Returns false if a job is already marked as running for this version.
    private bool TryStartJob(Guid versionId)
    {
        var key = JobKey(versionId);
        if (memoryCache.TryGetValue(key, out _))
            return false;

        memoryCache.Set(key, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = JobInFlightTtl,
            Size = 1
        });
        return true;
    }

    private void ClearJob(Guid versionId)
    {
        var key = JobKey(versionId);
        if (memoryCache.TryGetValue(key, out _))
            memoryCache.Remove(key);
    }

    /// <inheritdoc/>
    public async Task<PreviewResultDto?> GetPreviewUrlAsync(Guid versionId, Guid ownerId,
        CancellationToken ct = default)
    {
        var contentHash = await unitOfWork.Files.VersionBelongsToUserAsync(versionId, ownerId, ct);
        if (contentHash is null)
            throw new InvalidOperationException("No file found for preview generation");

        if (await unitOfWork.FileVersions.IsEncryptedAsync(versionId, ct)) return null;

        if (!await unitOfWork.FileVersions.IsPromotedAsync(versionId, ct))
            return new PreviewResultDto(null, null);

        var cachedUrl = await storageService.GetCachedPreview(versionId, PreviewKind.Preview, ct);
        if (cachedUrl is not null)
        {
            ClearJob(versionId);
            return new PreviewResultDto(cachedUrl, null);
        }

        //TODO: get only the mimetype from the version instead of the whole file
        var fileData = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Versions.Any(v => v.Id == versionId), ct);
        if (fileData is null) return null;

        var category = storageService.CategorizeFile(fileData.MimeType);

        switch (category)
        {
            case FileCategory.Image:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(
                        Encoding.UTF8.GetBytes(versionId.ToString()), $"image.{fileData.MimeType.Split('/')[1]}");
                return null;

            case FileCategory.Document:
            case FileCategory.Spreadsheet:
            case FileCategory.Presentation:
            case FileCategory.Pdf:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(
                        Encoding.UTF8.GetBytes(versionId.ToString()), $"document.{fileData.MimeType.Split('/')[1]}");
                return null;

            case FileCategory.Archive:
            {
                await using var seekableArchiveStream =
                    await storageService.DownloadSeekableFile(versionId, fileData.OwnerId, ct);

                var archivePreview = await archivePreviewService.GenerateArchivePreviewAsync(
                    seekableArchiveStream, fileData.Name, ct);

                return new PreviewResultDto(null, null, archivePreview.data);
            }

            case FileCategory.Text:
                var fileStream = await storageService.DownloadFile(versionId, fileData.OwnerId, ct);
                var (data, _) =
                    await textPreviewService.GenerateTextPreviewAsync(fileStream, fileData.Name, 524288, ct);
                return new PreviewResultDto(null, null, data);

            case FileCategory.Audio:
            case FileCategory.Video:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(
                        Encoding.UTF8.GetBytes(versionId.ToString()), $"media.{fileData.MimeType.Split('/')[1]}");
                return null;

            default:
                return new PreviewResultDto(null, null);
        }
    }

    /// <inheritdoc/>
    public async Task GeneratePreviewAsync(Guid versionId, Guid userId, PreviewKind kind = PreviewKind.Preview,
        CancellationToken ct = default)
    {
        var contentHash = await unitOfWork.Files.VersionBelongsToUserAsync(versionId, userId, ct);
        if (contentHash is null)
            throw new InvalidOperationException("No file found for preview generation");

        if (await unitOfWork.FileVersions.IsEncryptedAsync(versionId, ct))
            return;

        var cachedUrl = await storageService.GetCachedPreview(versionId, kind, ct);
        if (cachedUrl is not null)
        {
            ClearJob(versionId);
            return;
        }

        var fileData = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Versions.Any(v => v.Id == versionId), ct)
                       ?? throw new InvalidOperationException("No file found for preview generation");

        var category = storageService.CategorizeFile(fileData.MimeType);
        var subType = fileData.MimeType.Split('/')[1];

        switch (category)
        {
            case FileCategory.Image:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(Encoding.UTF8.GetBytes(versionId.ToString()),
                        $"image.{subType}");
                break;

            case FileCategory.Document:
            case FileCategory.Spreadsheet:
            case FileCategory.Presentation:
            case FileCategory.Pdf:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(Encoding.UTF8.GetBytes(versionId.ToString()),
                        $"document.{subType}");
                break;

            case FileCategory.Audio:
            case FileCategory.Video:
                if (TryStartJob(versionId))
                    await publisherService.PublishAsync(Encoding.UTF8.GetBytes(versionId.ToString()),
                        $"media.{subType}");
                break;

            case FileCategory.Archive:
            {
                await using var seekableArchiveStream =
                    await storageService.DownloadSeekableFile(versionId, fileData.OwnerId, ct);
                await archivePreviewService.GenerateArchivePreviewAsync(seekableArchiveStream, fileData.Name, ct);
                break;
            }

            case FileCategory.Text:
            {
                var fileStream = await storageService.DownloadFile(versionId, fileData.OwnerId, ct);
                await textPreviewService.GenerateTextPreviewAsync(fileStream, fileData.Name, 524288, ct);
                break;
            }

            case FileCategory.Unknown:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task<string?> GetThumbnailAsync(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var hash = await unitOfWork.Files.VersionBelongsToUserAsync(versionId, userId, ct);
        if (hash is null)
            throw new InvalidOperationException("No file found for preview generation");

        if (await unitOfWork.FileVersions.IsEncryptedAsync(versionId, ct)
            || !await unitOfWork.FileVersions.IsPromotedAsync(versionId, ct))
            return null;

        var cachedThumbnail = await storageService.GetCachedPreview(versionId, PreviewKind.Thumbnail, ct);
        if (cachedThumbnail is not null)
        {
            ClearJob(versionId);
            return cachedThumbnail;
        }

        // no thumbnail yet, either a pre-migration file or generation hasn't run,
        // trigger the same generation path GeneratePreviewAsync uses, it produces
        // preview + thumbnail together regardless of which one was actually missing
        await GeneratePreviewAsync(versionId, userId, PreviewKind.Thumbnail, ct);
        return null;
    }
}