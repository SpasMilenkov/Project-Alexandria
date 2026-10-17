using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;
using Alexandria.Services.Storage.MediaMetadata;
using Alexandria.Services.Storage.Playlists;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Previews;

/// <summary>
/// <c>PreviewBucket</c> derived artifacts (previews, thumbnails, their rows).
/// Mechanical extraction from <c>S3Service</c>; behavior preserved byte-for-byte, bugs included.
/// </summary>
public partial class PreviewArtifactService(
    IAmazonS3 s3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config,
    ILogger<PreviewArtifactService> logger,
    AuditContext auditContext,
    IUserSettingsService userSettingsService,
    IPublisherService publisher)
    : IPreviewArtifactService
{
    public async Task UploadPreview(
        string objectName,
        string contentType,
        Stream fileStream,
        Guid versionId,
        Guid uploadedBy,
        long contentLength,
        PreviewKind kind,
        CancellationToken ct = default)
    {
        var bucketName = config.Value.PreviewBucket;

        LogStartingPreviewUpload(logger, bucketName, objectName, versionId);

        auditContext.RunAsSystem();

        await unitOfWork.BeginTransactionAsync(ct);

        var version = await unitOfWork.FileVersions.GetByIdAsync(versionId, ct) ??
                      throw new InvalidOperationException("Version for preview not found");

        try
        {
            var filePath = $"{bucketName}/{objectName}";

            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = fileStream,
                ContentType = contentType,
                AutoCloseStream = false,
                DisableDefaultChecksumValidation = true,
            }, ct);

            var existingPreview =
                await unitOfWork.Previews.FirstOrDefaultAsync(p => p.VersionId == version.Id && p.Kind == kind, ct);

            Preview savedFile;

            if (existingPreview != null)
            {
                LogUpdatingExistingPreview(logger, existingPreview.Id, filePath);

                existingPreview.Size = contentLength;
                existingPreview.UpdatedBy = uploadedBy;
                existingPreview.ObjectKey = objectName;
                savedFile = await unitOfWork.Previews.UpdateAsync(existingPreview, ct);
            }
            else
            {
                LogCreatingNewPreview(logger, filePath, versionId);

                var fileEntity = new Preview
                {
                    Id = Guid.NewGuid(),
                    MimeType = contentType,
                    Size = contentLength,
                    UpdatedBy = uploadedBy,
                    VersionId = versionId,
                    Kind = kind,
                    ObjectKey = objectName
                };

                savedFile = await unitOfWork.Previews.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            LogPreviewUploadCompleted(logger, savedFile.Id, contentLength);
        }
        catch (Exception ex)
        {
            LogPreviewUploadFailed(logger, ex, bucketName, objectName, versionId);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                LogAttemptingPreviewCleanup(logger, bucketName, objectName);

                await s3.DeleteObjectAsync(bucketName, objectName, ct);

                LogPreviewCleanupSuccessful(logger, bucketName, objectName);
            }
            catch (Exception cleanupEx)
            {
                LogPreviewCleanupFailed(logger, cleanupEx, bucketName, objectName);
            }

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
        finally
        {
            await fileStream.DisposeAsync();
        }
    }

    internal static string GetMimeTypeFromFormat(string? formatName)
    {
        if (string.IsNullOrWhiteSpace(formatName))
            return "application/octet-stream";

        var f = formatName.ToLowerInvariant();

        if (f.Contains("webm") || f.Contains("matroska") || f.Contains("mkv"))
            return "video/webm";
        if (f.Contains("mp4") || f.Contains("mov") || f.Contains("m4v"))
            return "video/mp4";
        if (f.Contains("avi"))
            return "video/x-msvideo";
        if (f.Contains("mpeg") || f.Contains("mpg"))
            return "video/mpeg";
        if (f.Contains("mp3"))
            return "audio/mpeg";
        if (f.Contains("wav"))
            return "audio/wav";
        if (f.Contains("flac"))
            return "audio/flac";
        if (f.Contains("ogg"))
            return "audio/ogg";

        return "application/octet-stream";
    }

    /// <summary>
    ///  Takes in the preview video and the thumbnail streams, uploads them to storage and registers a metadata entity into
    ///  the database
    /// </summary>
    /// <param name="previewStream">The stream of the video preview</param>
    /// <param name="thumbnailStream">The thumbnail stream</param>
    /// <param name="metadataDto">The metadata DTO object returned from FFMPEG</param>
    /// <param name="previewSize">The size of the preview</param>
    /// <param name="thumbnailSize">The size of the thumbnail</param>
    /// <param name="objectName">The original object's name into the database</param>
    /// <param name="versionId">The version's entity ID inside the database</param>
    /// <param name="ct">Cancellation token</param>
    public async Task UploadMediaData(
        Stream previewStream,
        Stream thumbnailStream,
        long previewSize,
        long thumbnailSize,
        string objectName,
        Guid versionId,
        MediaMetadataDto metadataDto,
        CancellationToken ct = default)
    {
        var bucketName = config.Value.PreviewBucket ??
                         throw new InvalidOperationException("Preview bucket not configured");
        var previewKey = $"previews/{objectName}";
        var thumbnailKey = $"thumbnails/{objectName}";

        LogStartingMediaDataUpload(logger, versionId, previewKey, thumbnailKey);

        auditContext.RunAsSystem();

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var version = await unitOfWork.FileVersions.GetByIdAsync(versionId, ct) ??
                          throw new InvalidOperationException("File for preview not found");

            if (previewStream.CanSeek) previewStream.Position = 0;
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;

            LogUploadingPreviewVideo(logger, previewKey);

            // Preview upload
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = previewKey,
                InputStream = previewStream,
                ContentType = GetMimeTypeFromFormat(metadataDto.FormatName),
                DisableDefaultChecksumValidation = true,
                AutoCloseStream = false
            }, ct);

            LogUploadingThumbnail(logger, thumbnailKey);

            // Thumbnail upload
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = thumbnailKey,
                InputStream = thumbnailStream,
                ContentType = "image/jpeg",
                DisableDefaultChecksumValidation = true
            }, ct);

            // Media metadata, purely descriptive/technical fields now,
            // thumbnail location no longer tracked here, Previews owns that
            var existingMetadata =
                await unitOfWork.MediaMetadata.FirstOrDefaultAsync(f => f.FileId == version.FileId, ct);

            if (existingMetadata != null)
            {
                LogUpdatingMediaMetadata(logger, existingMetadata.Id, version.FileId);

                existingMetadata.Duration = metadataDto.Duration;
                existingMetadata.BitrateMbps = metadataDto.BitrateMbps;
                existingMetadata.FormatName = metadataDto.FormatName;
                existingMetadata.VideoCodec = metadataDto.VideoCodec;
                existingMetadata.AudioCodec = metadataDto.AudioCodec;
                existingMetadata.Width = metadataDto.Width;
                existingMetadata.Height = metadataDto.Height;
                existingMetadata.HasAudio = metadataDto.HasAudio;
                // Descriptive fields merge per-field: stored non-empty values (including
                // user corrections) survive automated output unless the owner opted into
                // automatic overwrite; empty fields always take it.
                var behavior = await userSettingsService.GetBehaviorAsync(version.File.OwnerId, ct);
                MediaMetadataMerge.ApplyDescriptiveFields(
                    existingMetadata, metadataDto, behavior.AllowAutomaticMetadataOverwrite);
                existingMetadata.UpdatedBy = SystemConfig.SystemId;

                await unitOfWork.MediaMetadata.UpdateAsync(existingMetadata, ct);
            }
            else
            {
                LogCreatingMediaMetadata(logger, version.FileId);
                var mediaMetadata = metadataDto.ToEntity(version.FileId);
                await unitOfWork.MediaMetadata.CreateAsync(mediaMetadata, ct);
            }

            // Preview record (the video/document preview object itself)
            var existingPreviewRecord = await unitOfWork.Previews.FirstOrDefaultAsync(
                f => f.VersionId == versionId && f.Kind == PreviewKind.Preview, ct);

            if (existingPreviewRecord != null)
            {
                LogUpdatingPreviewRecord(logger, existingPreviewRecord.Id);

                existingPreviewRecord.Size = previewSize;
                existingPreviewRecord.MimeType = GetMimeTypeFromFormat(metadataDto.FormatName);
                existingPreviewRecord.UpdatedBy = SystemConfig.SystemId;
                existingPreviewRecord.ObjectKey = previewKey;

                await unitOfWork.Previews.UpdateAsync(existingPreviewRecord, ct);
            }
            else
            {
                LogCreatingPreviewForMedia(logger, versionId);

                var preview = new Preview
                {
                    Id = Guid.NewGuid(),
                    MimeType = GetMimeTypeFromFormat(metadataDto.FormatName),
                    Size = previewSize,
                    Kind = PreviewKind.Preview,
                    UpdatedBy = SystemConfig.SystemId,
                    VersionId = versionId,
                    ObjectKey = previewKey
                };

                await unitOfWork.Previews.CreateAsync(preview, ct);
            }

            // Thumbnail record, separate row, separate Kind, same VersionId
            var existingThumbnailRecord = await unitOfWork.Previews.FirstOrDefaultAsync(
                f => f.VersionId == versionId && f.Kind == PreviewKind.Thumbnail, ct);

            if (existingThumbnailRecord != null)
            {
                LogUpdatingPreviewRecord(logger, existingThumbnailRecord.Id);

                existingThumbnailRecord.Size = thumbnailSize;
                existingThumbnailRecord.MimeType = "image/jpeg";
                existingThumbnailRecord.UpdatedBy = SystemConfig.SystemId;
                existingThumbnailRecord.ObjectKey = thumbnailKey;

                await unitOfWork.Previews.UpdateAsync(existingThumbnailRecord, ct);
            }
            else
            {
                LogCreatingPreviewForMedia(logger, versionId);

                var thumbnail = new Preview
                {
                    Id = Guid.NewGuid(),
                    MimeType = "image/jpeg",
                    Size = thumbnailSize,
                    Kind = PreviewKind.Thumbnail,
                    UpdatedBy = SystemConfig.SystemId,
                    VersionId = versionId,
                    ObjectKey = thumbnailKey
                };

                await unitOfWork.Previews.CreateAsync(thumbnail, ct);
            }

            await unitOfWork.CommitAsync(ct);

            await PlaylistSyncNotifier.PublishFileChangedAsync(
                publisher, version.FileId, version.File.OwnerId, logger);

            LogMediaDataUploadCompleted(logger, versionId, previewStream.Length);
        }
        catch (Exception e)
        {
            LogMediaDataUploadFailed(logger, e, versionId, previewKey, thumbnailKey);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                LogAttemptingMediaDataCleanup(logger, previewKey, thumbnailKey);

                await s3.DeleteObjectAsync(bucketName, previewKey, ct);
                await s3.DeleteObjectAsync(bucketName, thumbnailKey, ct);

                LogMediaDataCleanupSuccessful(logger, previewKey, thumbnailKey);
            }
            catch (Exception cleanupEx)
            {
                LogMediaDataCleanupFailed(logger, cleanupEx, previewKey, thumbnailKey);
            }

            throw;
        }
        finally
        {
            await previewStream.DisposeAsync();
            await thumbnailStream.DisposeAsync();
        }
    }

    public async Task<string?> GetCachedPreview(Guid versionId, PreviewKind kind, CancellationToken ct = default)
    {
        LogRetrievingCachedPreview(logger, versionId);

        var preview = await unitOfWork.Previews.FirstOrDefaultAsync(
            p => p.VersionId == versionId && p.Kind == kind, ct);

        if (preview is null)
        {
            LogNoPreviewAvailable(logger, versionId);
            return null;
        }

        // No presigning needed anymore — the previews bucket is website-mode,
        // and nginx's auth_request gate is what enforces ownership on every
        // read, cache hit or miss. This just needs to be a stable path.
        var kindSegment = kind == PreviewKind.Thumbnail ? "thumbnail" : "preview";
        return $"/preview/{versionId}/{kindSegment}";
    }

    public Task<PaginatedResult<UserPreviewDto>> GetMyPreviewsAsync(
        Guid userId, Guid? fileId, DateTime? createdBefore, int page, int pageSize,
        CancellationToken ct = default)
        => unitOfWork.Previews.GetListByUserAsync(userId, fileId, createdBefore, page, pageSize, ct);
}