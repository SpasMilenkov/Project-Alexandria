using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Queues;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;
using Amazon.S3;
using Amazon.S3.Model;
using Blake3;
using Cppl.Utilities.AWS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Directory = System.IO.Directory;
using FileEntity = Alexandria.Data.Models.File;

namespace Alexandria.Services.Storage;

public partial class S3Service(
    IAmazonS3 s3,
    [FromKeyedServices("public")] IAmazonS3 publicS3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config,
    ILogger<S3Service> logger,
    IPromotionQueue promotionQueue,
    IFileService fileService,
    AuditContext auditContext,
    IUserSettingsService userSettingsService,
    IPublisherService publisher)
    : IStorageService
{
    /// <summary>
    /// Deletes a temporary object from the temp bucket with error handling.
    /// Takes the bare temp object key; temp objects always live under the
    /// <c>content/</c> prefix, which is applied here.
    /// </summary>
    private async Task CleanupTempObjectAsync(string bucket, string key, CancellationToken ct)
    {
        var objectKey = key.StartsWith("content/", StringComparison.Ordinal) ? key : $"content/{key}";

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

    public FileCategory CategorizeFile(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return FileCategory.Unknown;

        return mimeType.ToLowerInvariant() switch
        {
            // ====== Images ======
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or "image/webp" or
                "image/bmp" or "image/tiff" or "image/svg+xml"
                => FileCategory.Image,

            // ====== Audio ======
            "audio/mpeg" or "audio/mp3" or "audio/wav" or "audio/ogg" or
                "audio/flac" or "audio/aac"
                => FileCategory.Audio,

            // ====== Video ======
            "video/mp4" or "video/x-msvideo" or "video/x-matroska" or
                "video/webm" or "video/quicktime"
                => FileCategory.Video,

            // ====== Documents (Word, OpenDoc, RTF, Plaintext) ======
            "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.oasis.opendocument.text" or "application/rtf"
                => FileCategory.Document,

            // ====== Spreadsheets ======
            "application/vnd.ms-excel" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                "application/vnd.oasis.opendocument.spreadsheet"
                => FileCategory.Spreadsheet,

            // ====== Presentations ======
            "application/vnd.ms-powerpoint"
                or "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
                "application/vnd.oasis.opendocument.presentation"
                => FileCategory.Presentation,

            // ====== PDF ======
            "application/pdf"
                => FileCategory.Pdf,

            // ====== Archives / Compressed ======
            "application/zip" or "application/x-7z-compressed" or
                "application/x-rar-compressed" or "application/gzip" or "application/x-tar" or "application/x-xz"
                => FileCategory.Archive,

            // ====== Text (Markdown, JSON, HTML, XML, etc.) ======
            "text/markdown" or "application/json" or "application/xml" or "text/xml" or "text/html" or "text/plain"
                => FileCategory.Text,

            // ====== Fallback ======
            _ => FileCategory.Unknown
        };
    }

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

        string bucket;
        string key;

        if (contentObject.IsPromoted)
        {
            bucket = config.Value.UploadBucket;
            key = contentObject.StorageKey;
        }
        else
        {
            var upload = await unitOfWork.Uploads.FirstOrDefaultAsync(
                             u => u.Id == contentObject.UploadId && u.Status == UploadStatus.Finished, ct)
                         ?? throw new InvalidOperationException(
                             $"No finished upload found for content object '{contentObjectId}'.");

            bucket = config.Value.TempBucket;
            key = $"content/{upload.TempObjectKey}";
        }

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
            var breakdown = await GetStorageBreakdown(userId, summary: true, ct: ct);
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
                await GenerateUploadUrl($"content/{tempObjectKey}", contentType, TimeSpan.FromSeconds(60)));
        }
        catch (Exception ex)
        {
            LogFileUploadInitiateFailed(logger, ex, tmp, tempObjectKey, ex.Message);

            await unitOfWork.RollbackAsync(ct);
            await CleanupTempObjectAsync(tmp, tempObjectKey, ct);

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
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
        if (config.Value.TempBucket is null)
            throw new InvalidOperationException("Temp bucket not configured");

        var tmp = config.Value.TempBucket;

        await unitOfWork.BeginTransactionAsync(ct);

        Upload? upload = null;

        try
        {
            await fileService.FolderWithOwnershipExistsAsync(directoryId, uploadedBy, ct);

            upload = await unitOfWork.Uploads
                .FirstOrDefaultAsync(u => u.Id == uploadId &&
                                          u.UserId == uploadedBy &&
                                          u.DeletedAt == null &&
                                          u.Status == UploadStatus.InProgress,
                    ct);
            if (upload is null)
                throw new InvalidOperationException("Upload not found");

            // =====================================================
            // 1. Stream object ONCE and compute hash + size
            // =====================================================
            byte[] computedHash;
            long computedSize;

            try
            {
                var result = await ComputeHashAndSizeFromS3Async(
                    tmp,
                    upload.TempObjectKey,
                    upload.Size > 0 ? (long?)upload.Size : null,
                    ct);

                computedHash = result.Hash;
                computedSize = result.Size;
            }
            catch (Exception ex)
            {
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);
                throw new InvalidOperationException(
                    $"Failed to read temp object: {ex.Message}", ex);
            }

            // =====================================================
            // 2. Validate size
            // =====================================================
            if ((BigInteger)computedSize > upload.Size)
            {
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);

                upload.Status = UploadStatus.Interrupted;
                upload.FinishedAt = DateTime.UtcNow;
                throw new InvalidOperationException(
                    $"Size mismatch: expected {upload.Size}, got {computedSize}");
            }

            // =====================================================
            // 3. Validate SHA256 against Upload entity
            // =====================================================
            if (!CryptographicOperations.FixedTimeEquals(computedHash, upload.Hash))
            {
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);

                upload.Status = UploadStatus.Interrupted;
                upload.FinishedAt = DateTime.UtcNow;

                throw new InvalidOperationException("SHA256 mismatch between upload and server");
            }

            upload.Status = UploadStatus.Finished;
            upload.FinishedAt = DateTime.UtcNow;

            // =====================================================
            // 4. Prepare hash string
            // =====================================================
            var serverHash = Convert
                .ToHexString(computedHash)
                .ToLowerInvariant();

            // =====================================================
            // 5. Deduplication: check content object
            // =====================================================
            var contentObject = !isEncrypted ? await unitOfWork.ContentObjects.HashExistsAsync(computedHash, ct) : null;

            if (contentObject is null)
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

            var wasOrphaned = contentObject.OrphanedAt is not null;
            var alreadyPromoted = contentObject.IsPromoted;

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


            // =====================================================
            // 6. Create / update file record
            // =====================================================
            var file = await unitOfWork.Files.FirstOrDefaultAsync(
                f =>
                    f.Name == objectName &&
                    f.DirectoryId == directoryId &&
                    f.OwnerId == uploadedBy,
                ct);

            if (file is null)
            {
                var result = await CreateFileWithVersionAsync(
                    objectName,
                    directoryId,
                    upload.MimeType,
                    uploadedBy,
                    computedSize,
                    Convert.FromHexString(serverHash),
                    encryptionHint: encryptionHint,
                    encryptionIv: encryptionIv,
                    encryptionSalt: encryptionSalt,
                    isEncrypted: isEncrypted,
                    integrityTag: integrityTag,
                    iterationCount: iterationCount,
                    contentObjectId: contentObject.Id,
                    ct: ct);
                await unitOfWork.CommitAsync(ct);

                if (alreadyPromoted)
                    // Bytes are already durable under this storage key. The new
                    // temp copy is redundant, so remove it instead of queueing
                    // a copy that the promotion worker would no-op.
                    await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);
                else
                    await promotionQueue.QueuePromotionAsync(contentObject.Id, upload.TempObjectKey, ct);

                return new UploadResult(
                    ObjectName: objectName,
                    Size: computedSize,
                    FileId: result.file.Id,
                    MimeType: result.file.MimeType,
                    result.fileVersion.Id,
                    false,
                    isEncrypted
                );
            }

            // =====================================================
            // 7. Create new version
            // =====================================================
            var currentVersion = await unitOfWork.FileVersions.GetByIdAsync(
                                     file.CurrentVersionId ?? Guid.Empty, ct) ??
                                 throw new InvalidOperationException("Current version missing");

            var newVersion = await unitOfWork.FileVersions.AddAsync(
                new FileVersion
                {
                    ContentHash = Convert.FromHexString(serverHash),
                    Size = computedSize,
                    VersionNumber = currentVersion.VersionNumber + 1,
                    MimeType = upload.MimeType,
                    CreatedBy = uploadedBy,
                    ContentObjectId = contentObject.Id,
                    FileId = file.Id,
                    IsEncrypted = isEncrypted,
                    EncryptionIv = encryptionIv,
                    EncryptionSalt = encryptionSalt,
                    IntegrityTag = integrityTag,
                    EncryptionHint = encryptionHint,
                    IterationCount = iterationCount,
                    KdfVersion = 1,
                },
                ct);

            file.CurrentVersionId = newVersion.Id;

            unitOfWork.Files.Update(file);

            await unitOfWork.CommitAsync(ct);

            if (alreadyPromoted)
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);
            else
                await promotionQueue.QueuePromotionAsync(contentObject.Id, upload.TempObjectKey, ct);

            LogUploadFinalized(logger, file.Id, computedSize);

            return new UploadResult(
                objectName,
                computedSize,
                file.Id,
                file.MimeType,
                newVersion.Id,
                true,
                isEncrypted);
        }
        catch (Exception ex)
        {
            LogFinalizeUploadFailed(logger, ex, objectName);

            await unitOfWork.RollbackAsync(ct);

            if (upload?.TempObjectKey is not null)
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
    }


    /// <summary>
    /// Streams the S3 object, computes SHA256 and exact size, and returns VersionId if present.
    /// </summary>
    private async Task<(byte[] Hash, long Size)> ComputeHashAndSizeFromS3Async(
        string bucket,
        string key,
        long? maxAllowedBytes,
        CancellationToken ct = default)
    {
        var req = new GetObjectRequest
        {
            BucketName = bucket,
            Key = $"content/{key}",
        };

        using var response = await s3.GetObjectAsync(req, ct);

        using var hasher = Hasher.New();

        var buffer = new byte[81920];
        long totalBytes = 0;
        int read;

        while ((read = await response.ResponseStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, read));

            totalBytes += read;
            if (maxAllowedBytes.HasValue && totalBytes > maxAllowedBytes.Value)
                throw new InvalidOperationException("File too large");
        }

        Hash hash = hasher.Finalize();
        byte[] hashBytes = hash.AsSpan().ToArray();

        return (hashBytes, totalBytes);
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

    public async Task<StorageBreakdown> GetStorageBreakdown(Guid userId, bool summary = false,
        CancellationToken ct = default)
    {
        var trashSize = await unitOfWork.Files.GetDeletedSizeAsync(userId, ct);
        var sizeByMimeType = await unitOfWork.Files.GetSizeByTypeAsync(userId, ct);
        IEnumerable<FileSummary> oldFiles = [];
        if (!summary)
            oldFiles = await unitOfWork.Files.GetOldFilesAsync(userId, ct);
        var (previewsSize, previewsCount, previewsByKind) =
            await unitOfWork.Previews.GetStorageByUserAsync(userId, ct);
        var (transcodedSize, representationsCount) =
            await unitOfWork.StreamingRepresentations.GetStorageByUserAsync(userId, ct);
        var user = await unitOfWork.Users.GetByIdAsync(userId, ct);

        var filesSize = sizeByMimeType.Values.Sum();
        var quotaBytes = user?.StorageQuota ?? 0;

        return new StorageBreakdown
        {
            OldFiles = oldFiles,
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

    public async Task<DownloadInfo> GetFilVersioneDownloadDetails(Guid versionId, Guid userId,
        CancellationToken ct = default)
    {
        var downloadInfo = await unitOfWork.FileVersions.GetDownloadMetadataAsync(versionId, userId, ct) ??
                           throw new InvalidOperationException("File not found");

        return new DownloadInfo
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