using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Microsoft.EntityFrameworkCore;
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

    // Creates (or requeues) the durable preview job and publishes its id.
    // Returns the job id, or null when equivalent work is already in flight.
    // Terminal jobs are requeued in place (same id, clean queued lifecycle)
    // instead of spawning a second job for the same version/kind/owner.
    private async Task<Guid?> DispatchPreviewJobAsync(
        Guid versionId, Guid userId, PreviewKind kind, string routingKey, JobType jobType,
        CancellationToken ct)
    {
        var existing = await unitOfWork.PreviewJobs.FirstOrDefaultAsync(
            j => j.VersionId == versionId && j.Kind == kind && j.UserId == userId && j.DeletedAt == null, ct);

        if (existing is not null)
        {
            if (existing.Job.Status is JobStatus.Queued or JobStatus.Processing)
                return null;

            await unitOfWork.Jobs.UpdateStatusAsync(existing.JobId, JobStatus.Queued, progress: 0, ct: ct);
            await unitOfWork.Jobs.ClearErrorAsync(existing.JobId, ct);
            await PublishOrFailAsync(existing.JobId, routingKey, ct);
            return existing.JobId;
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Queued,
            Type = jobType,
            UserId = userId
        };
        await unitOfWork.Jobs.AddAsync(job, ct);

        try
        {
            await unitOfWork.PreviewJobs.AddAsync(new PreviewJob
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                VersionId = versionId,
                Kind = kind,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        catch (DbUpdateException)
        {
            // Lost a dispatch race on the (version, kind, owner) unique index:
            // drop the orphan job and let the winner's delivery do the work.
            unitOfWork.Jobs.Remove(job);
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }

        await PublishOrFailAsync(job.Id, routingKey, ct);
        return job.Id;
    }

    private async Task PublishOrFailAsync(Guid jobId, string routingKey, CancellationToken ct)
    {
        try
        {
            await publisherService.PublishAsync(Encoding.UTF8.GetBytes(jobId.ToString()), routingKey);
        }
        catch (Exception)
        {
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: "publish failed", ct: ct);
            throw;
        }
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
                    await DispatchPreviewJobAsync(versionId, ownerId, PreviewKind.Preview,
                        $"image.{fileData.MimeType.Split('/')[1]}", JobType.MediaPreview, ct);
                return null;

            case FileCategory.Document:
            case FileCategory.Spreadsheet:
            case FileCategory.Presentation:
            case FileCategory.Pdf:
                if (TryStartJob(versionId))
                    await DispatchPreviewJobAsync(versionId, ownerId, PreviewKind.Preview,
                        $"document.{fileData.MimeType.Split('/')[1]}", JobType.DocumentPreview, ct);
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
                return new PreviewResultDto(null, data);

            case FileCategory.Audio:
            case FileCategory.Video:
                if (TryStartJob(versionId))
                    await DispatchPreviewJobAsync(versionId, ownerId, PreviewKind.Preview,
                        $"media.{fileData.MimeType.Split('/')[1]}", JobType.MediaPreview, ct);
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
                    await DispatchPreviewJobAsync(versionId, userId, kind,
                        $"image.{subType}", JobType.MediaPreview, ct);
                break;

            case FileCategory.Document:
            case FileCategory.Spreadsheet:
            case FileCategory.Presentation:
            case FileCategory.Pdf:
                if (TryStartJob(versionId))
                    await DispatchPreviewJobAsync(versionId, userId, kind,
                        $"document.{subType}", JobType.DocumentPreview, ct);
                break;

            case FileCategory.Audio:
            case FileCategory.Video:
                if (TryStartJob(versionId))
                    await DispatchPreviewJobAsync(versionId, userId, kind,
                        $"media.{subType}", JobType.MediaPreview, ct);
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