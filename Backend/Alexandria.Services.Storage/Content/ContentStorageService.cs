using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Mime;
using Alexandria.Common.Queues;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using Amazon.S3.Model;
using Blake3;
using Cppl.Utilities.AWS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Directory = System.IO.Directory;
using FileEntity = Alexandria.Data.Models.File;

namespace Alexandria.Services.Storage.Content;

public partial class ContentStorageService(
    IAmazonS3 s3,
    [FromKeyedServices("public")] IAmazonS3 publicS3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config,
    ILogger<ContentStorageService> logger,
    IPromotionQueue promotionQueue,
    IFileService fileService,
    AuditContext auditContext,
    IUserSettingsService userSettingsService,
    IPublisherService publisher,
    IMimeTypeResolver mimeResolver)
    : IContentStorageService
{
    // Jobs are dispatched as soon as an upload finalizes while promotion runs in the background, so a
    // worker can get here first. The limit covers two runs of the promotion scanner, for when the API
    // restarted and lost its in-memory queue.
    private static readonly TimeSpan PromotionWaitLimit = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PromotionPollInterval = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SingleUploadUrlLifetime = TimeSpan.FromSeconds(60);

    // Batched URLs wait for an upload slot. A URL that expires anyway is re-signed by the client.
    private static readonly TimeSpan BatchUploadUrlLifetime = TimeSpan.FromMinutes(2);

    private Task WaitUntilPromotedAsync(Guid contentObjectId, CancellationToken ct) =>
        WaitUntilPromotedAsync(contentObjectId, PromotionWaitLimit, PromotionPollInterval, ct);

    internal async Task WaitUntilPromotedAsync(
        Guid contentObjectId,
        TimeSpan limit,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        if (await unitOfWork.ContentObjects.IsPromotedAsync(contentObjectId, ct))
            return;

        LogWaitingForPromotion(logger, contentObjectId);

        var waited = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(pollInterval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            if (await unitOfWork.ContentObjects.IsPromotedAsync(contentObjectId, ct))
            {
                LogPromotionWaitEnded(logger, contentObjectId, waited.Elapsed);
                return;
            }

            if (waited.Elapsed >= limit)
            {
                LogPromotionWaitExpired(logger, contentObjectId, limit);
                throw new ContentNotPromotedException(contentObjectId, limit);
            }
        }
    }

    /// <summary>
    /// Deletes a temporary object from the temp bucket with error handling.
    /// Takes the bare temp object key; temp objects always live under the
    /// <c>content/</c> prefix, which is applied here.
    /// </summary>
    private async Task CleanupTempObjectAsync(string bucket, string key, CancellationToken ct)
    {
        var objectKey = key.StartsWith(TempObjectKeys.Sealed, StringComparison.Ordinal)
            ? key
            : $"{TempObjectKeys.Sealed}{key}";

        await DeleteTempObjectAsync(bucket, objectKey, ct);
    }

    private async Task DeleteTempObjectAsync(string bucket, string objectKey, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(bucket, objectKey, ct);
            LogTempObjectCleaned(logger, bucket, objectKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            LogTempObjectNotFound(logger, bucket, objectKey);
        }
        catch (Exception ex)
        {
            LogTempObjectCleanupFailed(logger, ex, bucket, objectKey);
        }
    }


    private async Task<(FileEntity file, FileVersion fileVersion)> CreateFileWithVersionAsync(string fileName,
        Guid? directoryId,
        string contentType,
        Guid uploadedBy,
        long size,
        byte[] hash,
        Guid contentObjectId,
        bool isEncrypted,
        byte[]? encryptionIv,
        byte[]? encryptionSalt,
        byte[]? integrityTag,
        string? encryptionHint,
        int? iterationCount,
        CancellationToken ct)
    {
        LogCreatingFileRecord(logger, fileName);

        var fileEntity = new FileEntity
        {
            Id = Guid.NewGuid(),
            Name = fileName,
            DirectoryId = directoryId,
            MimeType = contentType,
            UpdatedBy = uploadedBy,
            OwnerId = uploadedBy
        };
        var newFile = await unitOfWork.Files.CreateAsync(fileEntity, ct);

        var fileVersion = new FileVersion
        {
            ContentHash = hash,
            Size = size,
            VersionNumber = 1,
            MimeType = newFile.MimeType,
            CreatedBy = uploadedBy,
            ContentObjectId = contentObjectId,
            FileId = newFile.Id,
            IsEncrypted = isEncrypted,
            EncryptionIv = encryptionIv,
            EncryptionSalt = encryptionSalt,
            IntegrityTag = integrityTag,
            EncryptionHint = encryptionHint,
            IterationCount = iterationCount,
            KdfVersion = 1,
        };

        await unitOfWork.FileVersions.AddAsync(fileVersion, ct);
        fileEntity.CurrentVersionId = fileVersion.Id;
        unitOfWork.Files.Update(fileEntity);

        return (newFile, fileVersion);
    }

    /// <summary>
    /// Maps a MIME type to a category. The rules live in <see cref="MimeCategories"/> so the
    /// frontend catalog is generated from the same source.
    /// </summary>
    public FileCategory CategorizeFile(string mimeType) => MimeCategories.Categorize(mimeType);

    public async Task<string> GetFilePresignedUrl(Guid fileId, byte[] hash, string fileName, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.UploadBucket,
            Key = $"content/{Convert.ToHexStringLower(hash)}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTPS,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            }
        };

        if (await unitOfWork.Files.IsPromotedAsync(fileId)) return await publicS3.GetPreSignedURLAsync(request);

        var upload =
            await unitOfWork.Uploads.FirstOrDefaultAsync(u => u.Hash == hash && u.Status == UploadStatus.Finished) ??
            throw new InvalidOperationException("Upload hasn't finished yet");
        request.BucketName = config.Value.TempBucket;
        request.Key = $"content/{upload.TempObjectKey}";

        return await publicS3.GetPreSignedURLAsync(request);
    }

    public async Task<Stream> DownloadFile(Guid versionId, Guid userId, CancellationToken ct)
    {
        var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(
            v => v.Id == versionId && v.File.OwnerId == userId, ct);

        if (version is null)
        {
            LogFileNotFoundForDownload(logger, versionId);
            throw new InvalidOperationException($"File {versionId} not found in database.");
        }

        var hashString = Convert.ToHexStringLower(version.ContentHash);
        var objectName = $"content/{hashString}";
        try
        {
            var response = await s3.GetObjectAsync(config.Value.UploadBucket, objectName, ct);

            LogFileDownloadStreamAcquired(logger, config.Value.UploadBucket, objectName, response.ContentLength);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            LogS3ErrorDuringDownload(logger, ex, config.Value.UploadBucket, objectName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToDownloadFile(logger, ex, config.Value.UploadBucket, objectName);
            throw;
        }
    }

    public async Task<Stream> DownloadSeekableFile(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(
            v => v.Id == versionId && v.File.OwnerId == userId, ct);

        if (version is null)
        {
            LogFileNotFoundForDownload(logger, versionId);
            throw new InvalidOperationException($"File {versionId} not found in database.");
        }

        var hashString = Convert.ToHexStringLower(version.ContentHash);
        var objectName = $"content/{hashString}";

        try
        {
            var stream = new SeekableS3Stream(s3, config.Value.UploadBucket, objectName, 128 * 1024, 12);

            LogFileDownloadStreamAcquired(logger, config.Value.UploadBucket, objectName, stream.Length);

            return stream;
        }
        catch (AmazonS3Exception ex)
        {
            LogS3ErrorDuringDownload(logger, ex, config.Value.UploadBucket, objectName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToDownloadFile(logger, ex, config.Value.UploadBucket, objectName);
            throw;
        }
    }

    public async Task<Stream> DownloadSeekableFile(Guid versionId, CancellationToken ct = default)
    {
        var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(
            v => v.Id == versionId, ct);

        if (version is null)
        {
            LogFileNotFoundForDownload(logger, versionId);
            throw new InvalidOperationException($"File {versionId} not found in database.");
        }

        await WaitUntilPromotedAsync(version.ContentObjectId, ct);

        var hashString = Convert.ToHexStringLower(version.ContentHash);
        var objectName = $"content/{hashString}";

        try
        {
            var stream = new SeekableS3Stream(s3, config.Value.UploadBucket, objectName, 128 * 1024, 12);

            LogFileDownloadStreamAcquired(logger, config.Value.UploadBucket, objectName, stream.Length);

            return stream;
        }
        catch (AmazonS3Exception ex)
        {
            LogS3ErrorDuringDownload(logger, ex, config.Value.UploadBucket, objectName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToDownloadFile(logger, ex, config.Value.UploadBucket, objectName);
            throw;
        }
    }

    public async Task DownloadContentObjectAsync(
        Guid contentObjectId,
        string localFilePath,
        CancellationToken ct = default)
    {
        var contentObject = await unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct)
                            ?? throw new InvalidOperationException(
                                $"Content object '{contentObjectId}' was not found.");

        await WaitUntilPromotedAsync(contentObjectId, ct);

        var bucket = config.Value.UploadBucket;
        var key = contentObject.StorageKey;

        LogDownloadingContentObject(logger, contentObjectId, bucket, key);

        try
        {
            using var response = await s3.GetObjectAsync(bucket, key, ct);

            await using var localFile = new FileStream(
                localFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await response.ResponseStream.CopyToAsync(localFile, 81920, ct);

            LogContentObjectDownloaded(logger, contentObjectId, localFilePath);
        }
        catch (AmazonS3Exception ex)
        {
            LogContentObjectDownloadS3Error(logger, ex, contentObjectId, bucket, key, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogContentObjectDownloadFailed(logger, ex, contentObjectId, bucket, key);
            throw;
        }
    }

    public async Task UploadStreamingOutputAsync(
        string localDirectory,
        string keyPrefix,
        CancellationToken ct = default)
    {
        var bucket = config.Value.StreamingBucket
                     ?? throw new InvalidOperationException("Streaming bucket is not configured.");

        var files = Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories);

        LogUploadingStreamingOutput(logger, localDirectory, keyPrefix, files.Length);

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
                break;

            var relativePath = Path.GetRelativePath(localDirectory, file).Replace('\\', '/');
            var s3Key = $"{keyPrefix}/{relativePath}";
            var contentType = GetStreamingContentType(Path.GetExtension(file));

            try
            {
                await using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

                await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = s3Key,
                    InputStream = stream,
                    ContentType = contentType,
                    AutoCloseStream = false,
                    DisableDefaultChecksumValidation = true,
                }, ct);

                LogStreamingFilePut(logger, bucket, s3Key);
            }
            catch (Exception ex)
            {
                LogStreamingFilePutFailed(logger, ex, bucket, s3Key);
                throw;
            }
        }

        LogStreamingOutputUploaded(logger, keyPrefix, files.Length);
    }

    public async Task<string> GetStreamManifest(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var job = await unitOfWork.TranspilationJobs.GetByVersionId(versionId, userId, ct)
                  ?? throw new StreamingRepresentationNotFoundException(versionId);

        return $"{config.Value.BaseUrl}/stream/{job.SegmentPrefix}/dash/manifest.mpd";
    }

    internal static string GetStreamingContentType(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".m3u8" => "application/x-mpegURL",
            ".mpd" => "application/dash+xml",
            ".ts" => "video/mp2t",
            ".m4s" => "video/iso.segment",
            ".mp4" => "video/mp4",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };

    public async Task StreamFile(Guid versionId, Stream destination, CancellationToken ct)
    {
        LogStreamingFileToDestination(logger, versionId);

        var version = await unitOfWork.FileVersions.GetByIdAsync(versionId, ct);

        if (version is null)
        {
            LogFileNotFoundForStreaming(logger, versionId);
            throw new InvalidOperationException($"File with ID: {versionId} not found.");
        }

        await WaitUntilPromotedAsync(version.ContentObjectId, ct);

        var hash = Convert.ToHexStringLower(version.ContentHash);
        try
        {
            using var response = await s3.GetObjectAsync(config.Value.UploadBucket, $"content/{hash}", ct);

            LogStreamingFileContent(logger, versionId, response.ContentLength);

            await response.ResponseStream.CopyToAsync(destination, 81920, ct);

            LogFileStreamingCompleted(logger, versionId);
        }
        catch (AmazonS3Exception ex)
        {
            LogS3ErrorDuringStreaming(logger, ex, versionId, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToStreamFile(logger, ex, versionId);
            throw;
        }
    }

    /// <summary>
    /// Warn-only quota check: logs when an upload would exceed the user's quota but never
    /// blocks. Quota 0 means unlimited. Failures are swallowed so monitoring issues can
    /// never break uploads.
    /// TODO: block uploads over quota once quota UX ships.
    /// </summary>
    private async Task CheckStorageQuotaWarningAsync(Guid userId, long contentLength, CancellationToken ct)
    {
        try
        {
            var breakdown = await GetStorageBreakdown(userId, ct);
            if (breakdown.QuotaBytes > 0 && breakdown.UsedBytes + contentLength > breakdown.QuotaBytes)
                LogStorageQuotaExceeded(logger, userId, breakdown.UsedBytes, contentLength, breakdown.QuotaBytes);
        }
        catch (Exception ex)
        {
            LogStorageQuotaCheckFailed(logger, ex, userId);
        }
    }

    public async Task<(Guid, string)> InitiateFileUpload(
        string contentType,
        string clientHash,
        Guid userId,
        long contentLength,
        Guid? directoryId = null,
        CancellationToken ct = default
    )
    {
        if (config.Value.TempBucket is null) throw new InvalidOperationException();
        var tmp = config.Value.TempBucket;
        var tempObjectKey = Guid.NewGuid().ToString();

        await CheckStorageQuotaWarningAsync(userId, contentLength, ct);

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await fileService.FolderWithOwnershipExistsAsync(directoryId, userId, ct);

            var upload = new Upload
            {
                Id = Guid.NewGuid(),
                Status = UploadStatus.InProgress,
                Hash = Convert.FromHexString(clientHash),
                Size = contentLength,
                MimeType = contentType,
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                TempObjectKey = tempObjectKey,
            };

            await unitOfWork.Uploads.AddAsync(upload, ct);
            await unitOfWork.CommitAsync(ct);

            return (upload.Id,
                await GenerateUploadUrl($"{TempObjectKeys.Incoming}{tempObjectKey}", contentType, SingleUploadUrlLifetime));
        }
        catch (Exception ex)
        {
            LogFileUploadInitiateFailed(logger, ex, tmp, tempObjectKey, ex.Message);

            await unitOfWork.RollbackAsync(ct);
            await CleanupTempObjectAsync(tmp, tempObjectKey, ct);

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<UploadInitResult>> InitiateFileUploads(
        IReadOnlyList<UploadInitItem> items,
        Guid userId,
        CancellationToken ct = default)
    {
        if (config.Value.TempBucket is null)
            throw new InvalidOperationException("Temp bucket not configured");

        await CheckStorageQuotaWarningAsync(userId, items.Sum(item => item.ContentLength), ct);

        var unavailableDirectories = await FindUnavailableDirectoriesAsync(items, userId, ct);

        var uploads = new Upload?[items.Count];

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item.DirectoryId is { } directoryId && unavailableDirectories.Contains(directoryId))
                continue;

            uploads[i] = new Upload
            {
                Id = Guid.NewGuid(),
                Status = UploadStatus.InProgress,
                Hash = Convert.FromHexString(item.Hash),
                Size = item.ContentLength,
                MimeType = item.ContentType,
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                TempObjectKey = Guid.NewGuid().ToString(),
            };
        }

        var accepted = uploads.OfType<Upload>().ToList();

        if (accepted.Count > 0)
            await AddUploadsAsync(accepted, ct);

        var results = new List<UploadInitResult>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (uploads[i] is not { } upload)
            {
                results.Add(new UploadInitResult(null, null, UploadErrorMessages.DestinationNotFound));
                continue;
            }

            var url = await GenerateUploadUrl(
                $"{TempObjectKeys.Incoming}{upload.TempObjectKey}",
                items[i].ContentType,
                BatchUploadUrlLifetime);

            results.Add(new UploadInitResult(upload.Id, url, null));
        }

        return results;
    }

    private async Task<HashSet<Guid>> FindUnavailableDirectoriesAsync(
        IReadOnlyList<UploadInitItem> items,
        Guid userId,
        CancellationToken ct)
    {
        var unavailable = new HashSet<Guid>();

        foreach (var directoryId in items.Select(item => item.DirectoryId).OfType<Guid>().Distinct())
        {
            try
            {
                await fileService.FolderWithOwnershipExistsAsync(directoryId, userId, ct);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogUploadDirectoryUnavailable(logger, ex, directoryId, userId);
                unavailable.Add(directoryId);
            }
        }

        return unavailable;
    }

    // Validated rows can't fail this insert individually, so a failure here is an infrastructure
    // fault that would hit every item anyway. Nothing is created and the whole request fails.
    private async Task AddUploadsAsync(List<Upload> uploads, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            await unitOfWork.Uploads.AddRangeAsync(uploads, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            LogUploadBatchInitiateFailed(logger, ex, uploads.Count);
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<UploadResult> FinalizeFileUpload(string objectName,
        Guid uploadId,
        Guid uploadedBy,
        byte[]? encryptionIv,
        byte[]? encryptionSalt,
        byte[]? integrityTag,
        string? encryptionHint,
        int? iterationCount,
        bool isEncrypted = false,
        Guid? directoryId = null,
        CancellationToken ct = default)
    {
        VerifiedUpload? verified = null;

        try
        {
            verified = await VerifyUploadAsync(uploadId, uploadedBy, directoryId, ct);

            return await CommitUploadAsync(
                verified,
                new UploadFinalizeItem
                {
                    UploadId = uploadId,
                    DirectoryId = directoryId,
                    FileName = objectName,
                    IsEncrypted = isEncrypted,
                    EncryptionIv = encryptionIv,
                    EncryptionSalt = encryptionSalt,
                    IntegrityTag = integrityTag,
                    EncryptionHint = encryptionHint,
                    IterationCount = iterationCount
                },
                ct);
        }
        catch (Exception ex)
        {
            LogFinalizeUploadFailed(logger, ex, objectName);

            if (verified is not null)
                await AbandonUploadAsync(uploadId, verified.TempObjectKey, CancellationToken.None);

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
    }

    public async Task<VerifiedUpload> VerifyUploadAsync(
        Guid uploadId,
        Guid uploadedBy,
        Guid? directoryId,
        CancellationToken ct = default)
    {
        var tmp = config.Value.TempBucket
                  ?? throw new InvalidOperationException("Temp bucket not configured");

        await fileService.FolderWithOwnershipExistsAsync(directoryId, uploadedBy, ct);

        if (!await unitOfWork.Uploads.TryClaimForFinalizeAsync(uploadId, uploadedBy, DateTime.UtcNow, ct))
            throw new InvalidOperationException("Upload not found");

        var upload = await unitOfWork.Uploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new InvalidOperationException("Upload not found");

        try
        {
            await SealUploadAsync(tmp, upload.TempObjectKey, ct);

            var scan = await ComputeHashAndSizeFromS3Async(
                tmp,
                upload.TempObjectKey,
                upload.Size > 0 ? upload.Size : null,
                ct);

            if (scan.Size > upload.Size)
                throw new UploadVerificationException($"Size mismatch: expected {upload.Size}, got {scan.Size}");

            if (!CryptographicOperations.FixedTimeEquals(scan.Hash, upload.Hash))
                throw new UploadVerificationException("Hash mismatch between upload and server");

            return new VerifiedUpload
            {
                UploadId = upload.Id,
                UploadedBy = uploadedBy,
                TempObjectKey = upload.TempObjectKey,
                Hash = scan.Hash,
                Size = scan.Size,
                Header = scan.Header
            };
        }
        catch (Exception ex)
        {
            LogUploadVerificationFailed(logger, ex, uploadId);

            await AbandonUploadAsync(uploadId, upload.TempObjectKey, CancellationToken.None);

            throw;
        }
    }

    private async Task SealUploadAsync(string bucket, string tempObjectKey, CancellationToken ct)
    {
        var incomingKey = $"{TempObjectKeys.Incoming}{tempObjectKey}";

        try
        {
            await s3.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = bucket,
                SourceKey = incomingKey,
                DestinationBucket = bucket,
                DestinationKey = $"{TempObjectKeys.Sealed}{tempObjectKey}"
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UploadVerificationException("The uploaded object was not found", ex);
        }

        await DeleteTempObjectAsync(bucket, incomingKey, ct);
    }

    public async Task<UploadResult> CommitUploadAsync(
        VerifiedUpload verified,
        UploadFinalizeItem item,
        CancellationToken ct = default)
    {
        var uploadId = verified.UploadId;
        var uploadedBy = verified.UploadedBy;
        var objectName = item.FileName;
        var directoryId = item.DirectoryId;
        var isEncrypted = item.IsEncrypted;
        var computedHash = verified.Hash;
        var computedSize = verified.Size;

        ContentObject contentObject;
        bool alreadyPromoted;
        UploadResult result;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var upload = await unitOfWork.Uploads
                             .FirstOrDefaultAsync(u => u.Id == uploadId &&
                                                       u.UserId == uploadedBy &&
                                                       u.DeletedAt == null &&
                                                       u.Status == UploadStatus.Finalizing,
                                 ct)
                         ?? throw new InvalidOperationException("Upload not found");

            upload.Status = UploadStatus.Finished;
            upload.FinishedAt = DateTime.UtcNow;

            // Encrypted payloads are ciphertext and are never sniffed. Upload.MimeType keeps the
            // declared value as an audit trail. The sniffer swallows its own faults, so this
            // step can never reject an upload.
            var resolution = isEncrypted
                ? mimeResolver.ResolveWithoutContent(objectName, upload.MimeType)
                : mimeResolver.Resolve(objectName, verified.Header, upload.MimeType);
            var resolvedMimeType = resolution.MimeType;

            var declaredMimeType = MimeAliases.Canonicalize(upload.MimeType);
            if (MimeAliases.IsUsable(declaredMimeType) && declaredMimeType != resolvedMimeType)
                LogMimeCorrected(logger, upload.Id, declaredMimeType, resolvedMimeType, resolution.Source);

            var serverHash = Convert
                .ToHexString(computedHash)
                .ToLowerInvariant();

            var existing = !isEncrypted ? await unitOfWork.ContentObjects.HashExistsAsync(computedHash, ct) : null;

            if (existing is null)
            {
                contentObject = new ContentObject
                {
                    Hash = computedHash,
                    StorageKey = $"content/{serverHash}",
                    UpdatedBy = uploadedBy,
                    IsPromoted = false,
                    PromotionAttempts = 0,
                    UploadId = upload.Id
                };

                await unitOfWork.ContentObjects.AddAsync(contentObject, ct);
            }
            else
            {
                contentObject = existing;
            }

            var wasOrphaned = contentObject.OrphanedAt is not null;
            alreadyPromoted = contentObject.IsPromoted;

            if (wasOrphaned)
            {
                contentObject.OrphanedAt = null;

                // Content objects are never soft-deleted today, but the planned
                // unique index permits a soft-deleted plus active pair sharing
                // one hash. Clearing both keeps a single live row per hash.
                contentObject.DeletedAt = null;
                contentObject.UploadId = upload.Id;
                contentObject.UpdatedBy = uploadedBy;

                if (!contentObject.IsPromoted)
                {
                    // Otherwise the promotion scanner keeps ignoring this row:
                    // it only picks up objects below the attempt budget.
                    contentObject.PromotionAttempts = 0;
                    contentObject.LastPromotionAttemptAt = null;
                }

                unitOfWork.ContentObjects.Update(contentObject);
            }

            var file = await unitOfWork.Files.FirstOrDefaultAsync(
                f =>
                    f.Name == objectName &&
                    f.DirectoryId == directoryId &&
                    f.OwnerId == uploadedBy,
                ct);

            if (file is null)
            {
                var created = await CreateFileWithVersionAsync(
                    objectName,
                    directoryId,
                    resolvedMimeType,
                    uploadedBy,
                    computedSize,
                    Convert.FromHexString(serverHash),
                    encryptionHint: item.EncryptionHint,
                    encryptionIv: item.EncryptionIv,
                    encryptionSalt: item.EncryptionSalt,
                    isEncrypted: isEncrypted,
                    integrityTag: item.IntegrityTag,
                    iterationCount: item.IterationCount,
                    contentObjectId: contentObject.Id,
                    ct: ct);

                result = new UploadResult(
                    ObjectName: objectName,
                    Size: computedSize,
                    FileId: created.file.Id,
                    MimeType: created.file.MimeType,
                    created.fileVersion.Id,
                    false,
                    isEncrypted);
            }
            else
            {
                var currentVersion = await unitOfWork.FileVersions.GetByIdAsync(
                                         file.CurrentVersionId ?? Guid.Empty, ct) ??
                                     throw new InvalidOperationException("Current version missing");

                var newVersion = await unitOfWork.FileVersions.AddAsync(
                    new FileVersion
                    {
                        ContentHash = Convert.FromHexString(serverHash),
                        Size = computedSize,
                        VersionNumber = currentVersion.VersionNumber + 1,
                        MimeType = resolvedMimeType,
                        CreatedBy = uploadedBy,
                        ContentObjectId = contentObject.Id,
                        FileId = file.Id,
                        IsEncrypted = isEncrypted,
                        EncryptionIv = item.EncryptionIv,
                        EncryptionSalt = item.EncryptionSalt,
                        IntegrityTag = item.IntegrityTag,
                        EncryptionHint = item.EncryptionHint,
                        IterationCount = item.IterationCount,
                        KdfVersion = 1,
                    },
                    ct);

                file.CurrentVersionId = newVersion.Id;
                file.MimeType = resolvedMimeType;

                unitOfWork.Files.Update(file);

                result = new UploadResult(
                    objectName,
                    computedSize,
                    file.Id,
                    file.MimeType,
                    newVersion.Id,
                    true,
                    isEncrypted);
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            LogUploadCommitFailed(logger, ex, uploadId);

            await unitOfWork.RollbackAsync(CancellationToken.None);

            throw;
        }

        // The upload is committed, so these ignore a client disconnect. Failing here would make
        // the caller abandon an upload that has already been saved.
        if (alreadyPromoted)
            // Bytes are already durable under this storage key. The new temp copy is
            // redundant, so remove it instead of queueing a copy the worker would no-op.
            await CleanupTempObjectAsync(config.Value.TempBucket, verified.TempObjectKey, CancellationToken.None);
        else
            await promotionQueue.QueuePromotionAsync(contentObject.Id, verified.TempObjectKey, CancellationToken.None);

        LogUploadFinalized(logger, result.FileId, computedSize);

        return result;
    }

    public async Task AbandonUploadAsync(Guid uploadId, string tempObjectKey, CancellationToken ct = default)
    {
        var tmp = config.Value.TempBucket
                  ?? throw new InvalidOperationException("Temp bucket not configured");

        await CleanupTempObjectAsync(tmp, tempObjectKey, ct);
        await DeleteTempObjectAsync(tmp, $"{TempObjectKeys.Incoming}{tempObjectKey}", ct);

        try
        {
            await unitOfWork.Uploads.MarkInterruptedAsync(uploadId, DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            // The sweeper retires a stranded Finalizing row once it reaches the abandoned age.
            LogUploadAbandonFailed(logger, ex, uploadId);
        }
    }


    /// <summary>Result of the single pass over an uploaded object.</summary>
    private sealed record ScanResult(byte[] Hash, long Size, byte[] Header);

    /// <summary>
    /// Streams the S3 object once and computes the hash, the exact size and the first
    /// <see cref="MimeSniffing.HeaderBytes"/> bytes used for content sniffing.
    /// </summary>
    private async Task<ScanResult> ComputeHashAndSizeFromS3Async(
        string bucket,
        string key,
        long? maxAllowedBytes,
        CancellationToken ct = default)
    {
        var req = new GetObjectRequest
        {
            BucketName = bucket,
            Key = $"{TempObjectKeys.Sealed}{key}",
        };

        using var response = await s3.GetObjectAsync(req, ct);

        using var hasher = Hasher.New();
        var headerBuffer = new HeaderBuffer(MimeSniffing.HeaderBytes);

        var buffer = new byte[81920];
        long totalBytes = 0;
        int read;

        while ((read = await response.ResponseStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, read));
            headerBuffer.Append(buffer.AsSpan(0, read));

            totalBytes += read;
            if (maxAllowedBytes.HasValue && totalBytes > maxAllowedBytes.Value)
                throw new UploadVerificationException("File too large");
        }

        Hash hash = hasher.Finalize();
        byte[] hashBytes = hash.AsSpan().ToArray();

        return new ScanResult(hashBytes, totalBytes, headerBuffer.ToArray());
    }


    private async Task<string> GenerateUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.TempBucket,
            Key = $"{objectKey}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTPS,
            ContentType = contentType,
        };

        return await publicS3.GetPreSignedURLAsync(request);
    }

    public async Task DeleteStreamingOutputByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var paginator = s3.Paginators.ListObjectsV2(new ListObjectsV2Request
        {
            BucketName = config.Value.StreamingBucket,
            Prefix = prefix
        });

        var batch = new List<KeyVersion>();

        await foreach (var obj in paginator.S3Objects.WithCancellation(ct))
        {
            batch.Add(new KeyVersion { Key = obj.Key });

            if (batch.Count == 1000)
            {
                await FlushBatchAsync(batch, ct);
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, ct);
        }
    }

    private async Task FlushBatchAsync(List<KeyVersion> batch, CancellationToken ct)
    {
        await s3.DeleteObjectsAsync(new DeleteObjectsRequest
        {
            BucketName = config.Value.StreamingBucket,
            Objects = batch
        }, ct);

        batch.Clear();
    }

    public async Task<StorageBreakdown> GetStorageBreakdown(Guid userId, CancellationToken ct = default)
    {
        var trashSize = await unitOfWork.Files.GetDeletedSizeAsync(userId, ct);
        var sizeByMimeType = await unitOfWork.Files.GetSizeByTypeAsync(userId, ct);
        var (previewsSize, previewsCount, previewsByKind) =
            await unitOfWork.Previews.GetStorageByUserAsync(userId, ct);
        var (transcodedSize, representationsCount) =
            await unitOfWork.StreamingRepresentations.GetStorageByUserAsync(userId, ct);
        var user = await unitOfWork.Users.GetByIdAsync(userId, ct);

        var filesSize = sizeByMimeType.Values.Sum();
        var quotaBytes = user?.StorageQuota ?? 0;

        return new StorageBreakdown
        {
            SizeByType = sizeByMimeType,
            TrashSize = trashSize,
            FilesSize = filesSize,
            PreviewsSize = previewsSize,
            PreviewsCount = previewsCount,
            PreviewsByKind = previewsByKind,
            TranscodedSize = transcodedSize,
            RepresentationsCount = representationsCount,
            QuotaBytes = quotaBytes,
            UsedBytes = filesSize + previewsSize + transcodedSize
        };
    }

    public async Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default)
    {
        var downloadInfo = await unitOfWork.FileVersions.GetVersionDownloadInfoAsync(fileVersionId, ownerId, ct) ??
                           throw new InvalidOperationException("Version not found");

        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.UploadBucket,
            Key = $"content/{Convert.ToHexStringLower(downloadInfo.Hash)}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Protocol = Protocol.HTTPS,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{downloadInfo.FileName}\""
            }
        };

        if (await unitOfWork.FileVersions.IsPromotedAsync(fileVersionId, ct))
            return await publicS3.GetPreSignedURLAsync(request);

        var upload = await unitOfWork.Uploads.FirstOrDefaultAsync(
                         u => u.Hash == downloadInfo.Hash && u.Status == UploadStatus.Finished, ct) ??
                     throw new InvalidOperationException("Upload record not found for unpromoted version");

        request.BucketName = config.Value.TempBucket;
        request.Key = $"content/{upload.TempObjectKey}";

        return await publicS3.GetPreSignedURLAsync(request);
    }

    public async Task<DownloadInfo> GetFileDownloadDetails(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        (DownloadMetadata downloadMetadata, byte[] fileHash) =
            await unitOfWork.Files.GetDownloadMetadataAsync(fileId, userId, ct) ??
            throw new InvalidOperationException("File not found");

        return new DownloadInfo
        {
            PresignedUrl =
                await GetFilePresignedUrl(fileId, fileHash, downloadMetadata.FileName, TimeSpan.FromMinutes(10)),
            FileName = downloadMetadata.FileName,
            MimeType = downloadMetadata.MimeType,
            EncryptionHint = downloadMetadata.EncryptionHint,
            EncryptionIv = downloadMetadata.EncryptionIv,
            EncryptionSalt = downloadMetadata.EncryptionSalt,
            IntegrityTag = downloadMetadata.IntegrityTag,
            IsEncrypted = downloadMetadata.IsEncrypted,
            IterationCount = downloadMetadata.IterationCount
        };
    }

    public async Task<VersionDownload> GetFilVersioneDownloadDetails(Guid versionId, Guid userId,
        CancellationToken ct = default)
    {
        var (downloadInfo, fileId, versionNumber) =
            await unitOfWork.FileVersions.GetDownloadMetadataAsync(versionId, userId, ct) ??
            throw new InvalidOperationException("File not found");

        var info = new DownloadInfo
        {
            PresignedUrl = await GetVersionPresignedUrl(versionId, userId, ct),
            FileName = downloadInfo.FileName,
            MimeType = downloadInfo.MimeType,
            EncryptionHint = downloadInfo.EncryptionHint,
            EncryptionIv = downloadInfo.EncryptionIv,
            EncryptionSalt = downloadInfo.EncryptionSalt,
            IntegrityTag = downloadInfo.IntegrityTag,
            IsEncrypted = downloadInfo.IsEncrypted,
            IterationCount = downloadInfo.IterationCount
        };

        return new VersionDownload(info, fileId, versionNumber);
    }

    public async Task StreamBulkZipAsync(
        Guid[] directoryIds,
        Guid[] fileIds,
        Guid userId,
        Stream destination, // HttpContext.Response.Body
        CancellationToken ct = default)
    {
        var entries = await unitOfWork.Directories.GetBulkDownloadEntriesAsync(
            directoryIds, fileIds, userId, ct);

        // Sidecar: collect encrypted file metadata so the client
        // knows what to decrypt — written as a single JSON entry
        var encryptedMeta = new List<object>();

        // ZipArchive on a non-seekable stream automatically uses
        // data descriptors for each entry; central directory is
        // flushed on Dispose() — fully standard, every unzipper handles it
        await using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) break;

            var zipPath = string.IsNullOrEmpty(entry.DirectoryPath)
                ? entry.FileName
                : $"{entry.DirectoryPath}/{entry.FileName}";

            if (entry.IsEncrypted)
            {
                // Include the raw ciphertext under its natural path;
                // decryption params go into the sidecar
                encryptedMeta.Add(new
                {
                    Path = zipPath,
                    entry.EncryptionHint,
                    Iv = entry.EncryptionIv is null ? null : Convert.ToBase64String(entry.EncryptionIv),
                    Salt = entry.EncryptionSalt is null ? null : Convert.ToBase64String(entry.EncryptionSalt),
                    Tag = entry.IntegrityTag is null ? null : Convert.ToBase64String(entry.IntegrityTag),
                    entry.IterationCount
                });
            }

            // Resolve bucket + key
            var (bucket, key) = entry.IsPromoted
                ? (config.Value.UploadBucket, entry.StorageKey) // "content/abc123"
                : (config.Value.TempBucket, $"content/{entry.TempObjectKey}");

            var zipEntry = archive.CreateEntry(zipPath, GetCompressionLevel(entry.FileName));

            try
            {
                await using var entryStream = zipEntry.Open();

                using var s3Response = await s3.GetObjectAsync(bucket, key, ct);
                await s3Response.ResponseStream.CopyToAsync(entryStream, 81920, ct);
                // S3 connection released here; next file fetched next iteration
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Don't abort the whole zip for one missing object —
                // write a zero-byte placeholder so the user knows
                logger.LogWarning(ex, "S3 object not found during bulk zip: {Bucket}/{Key}", bucket, key);
            }
        }

        // Write sidecar at the very end — it lands before the central directory
        if (encryptedMeta.Count > 0)
        {
            var sidecar = archive.CreateEntry("_encrypted_metadata.json", CompressionLevel.Fastest);
            await using var sidecarStream = sidecar.Open();
            await JsonSerializer.SerializeAsync(sidecarStream, encryptedMeta, cancellationToken: ct);
        }

        // Dispose flushes the central directory → response stream closed b y Kestrel
    }

    internal static CompressionLevel GetCompressionLevel(string fileName)
    {
        // These formats are already compressed — deflating them again
        // wastes CPU and barely reduces size
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"
            or ".mp4" or ".mkv" or ".webm" or ".mov"
            or ".mp3" or ".flac" or ".aac" or ".ogg"
            or ".zip" or ".7z" or ".gz" or ".rar"
            ? CompressionLevel.NoCompression
            : CompressionLevel.Fastest;
    }
}