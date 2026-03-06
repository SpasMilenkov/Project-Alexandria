using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Blake3;
using Common;
using Common.Config;
using Common.Queues;
using Common.Services;
using Cppl.Utilities.AWS;
using DTO.Extensions;
using DTO.Files;
using DTO.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Enumerators;
using MediaMetadata = DTO.Files.MediaMetadata;
using FileEntity = Models.File;

namespace Storage;

public partial class S3Service(
    IAmazonS3 s3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config,
    ILogger<S3Service> logger,
    IPromotionQueue promotionQueue,
    IFileService fileService)
    : IStorageService
{
    /// <summary>
    /// Deletes a temporary object from the temp bucket with error handling.
    /// </summary>
    private async Task CleanupTempObjectAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(bucket, key, ct);
            LogTempObjectCleaned(logger, bucket, key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            LogTempObjectNotFound(logger, bucket, key);
        }
        catch (Exception ex)
        {
            LogTempObjectCleanupFailed(logger, ex, bucket, key);
            // Don't throw - cleanup is best-effort
        }
    }


    private async Task<(FileEntity file, FileVersion fileVersion)> CreateFileWithVersionAsync(string fileName,
        Guid? directoryId,
        string contentType,
        Guid uploadedBy,
        long size,
        byte[] hash,
        Guid contentObjectId,
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
            FileId = newFile.Id
        };

        await unitOfWork.FileVersions.AddAsync(fileVersion, ct);
        fileEntity.CurrentVersionId = fileVersion.Id;
        unitOfWork.Files.Update(fileEntity);

        return (newFile, fileVersion);
    }

    private async Task<UploadResult> CleanupAndReturnResultAsync(
        string tmpBucket, string objectName, string serverHash,
        long size, Guid fileId, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(tmpBucket, objectName, ct);
        return new UploadResult(
            objectName,
            serverHash,
            size,
            fileId);
    }

    public async Task<UploadResult> UploadPreview(
        string bucketName,
        string objectName,
        string contentType,
        Stream fileStream,
        Guid originalFileId,
        Guid uploadedBy,
        long contentLength = -1,
        string? originalFileName = null,
        CancellationToken ct = default)
    {
        LogStartingPreviewUpload(logger, bucketName, objectName, originalFileId);

        await unitOfWork.BeginTransactionAsync(ct);

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

            var existingFile = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            Preview savedFile;

            if (existingFile != null)
            {
                LogUpdatingExistingPreview(logger, existingFile.Id, filePath);

                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger();
                existingFile.UpdatedBy = uploadedBy;

                savedFile = await unitOfWork.Previews.UpdateAsync(existingFile, ct);
            }
            else
            {
                LogCreatingNewPreview(logger, filePath, originalFileId);

                var fileEntity = new Preview
                {
                    Id = Guid.NewGuid(),
                    Name = originalFileName ?? objectName,
                    Path = filePath,
                    MimeType = contentType,
                    Size = new BigInteger(contentLength),
                    UpdatedBy = uploadedBy,
                    FileId = originalFileId
                };

                savedFile = await unitOfWork.Previews.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            LogPreviewUploadCompleted(logger, savedFile.Id, contentLength);

            return new UploadResult(
                objectName,
                "",
                contentLength,
                savedFile.Id);
        }
        catch (Exception ex)
        {
            LogPreviewUploadFailed(logger, ex, bucketName, objectName, originalFileId);

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
    }

    private static string GetMimeTypeFromFormat(string? formatName)
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
    ///     Takes in the preview video and the thumbnail streams, uploads them to storage and registers a metadata entity into
    ///     the database
    /// </summary>
    /// <param name="previewStream">The stream of the video preview</param>
    /// <param name="thumbnailStream">The thumbnail stream</param>
    /// <param name="metadataDto">The metadata DTO object returned from FFMPEG</param>
    /// <param name="objectName">The original object's name into the database</param>
    /// <param name="fileId">The original file's entity ID inside the database</param>
    /// <param name="ct">Cancellation token</param>
    public async Task UploadMediaData(Stream previewStream, Stream thumbnailStream,
        string objectName, Guid fileId, MediaMetadata metadataDto,
        CancellationToken ct = default)
    {
        var bucketName = config.Value.PreviewBucket ??
                         throw new InvalidOperationException("Preview bucket not configured");
        var filePath = $"{bucketName}/{objectName}";
        var previewKey = $"previews/{objectName}";
        var thumbnailKey = $"thumbnails/{fileId}.jpg";

        LogStartingMediaDataUpload(logger, fileId, previewKey, thumbnailKey);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var originalFileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId, ct);

            if (!originalFileExists)
            {
                LogOriginalFileNotFoundForMedia(logger, fileId);
                throw new InvalidOperationException($"Original file with ID {fileId} not found");
            }

            if (previewStream.CanSeek) previewStream.Position = 0;
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;

            LogUploadingPreviewVideo(logger, previewKey);

            // Preview upload
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = $"previews/{objectName}",
                InputStream = previewStream,
                ContentType = GetMimeTypeFromFormat(metadataDto.FormatName),
                DisableDefaultChecksumValidation = true,
                AutoCloseStream = false
            }, ct);

            // Reset thumbnail stream if needed
            if (thumbnailStream.CanSeek)
                thumbnailStream.Position = 0;

            LogUploadingThumbnail(logger, thumbnailKey);

            // Thumbnail upload
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = $"thumbnails/{objectName}",
                InputStream = thumbnailStream,
                ContentType = "image/jpeg",
                DisableDefaultChecksumValidation = true
            }, ct);


            var existingMetadata = await unitOfWork.MediaMetadata.FirstOrDefaultAsync(f => f.FileId == fileId, ct);

            if (existingMetadata != null)
            {
                LogUpdatingMediaMetadata(logger, existingMetadata.Id, fileId);

                existingMetadata.Duration = metadataDto.Duration;
                existingMetadata.BitrateMbps = metadataDto.BitrateMbps;
                existingMetadata.FormatName = metadataDto.FormatName;
                existingMetadata.ThumbnailPath = $"{bucketName}/{thumbnailKey}";
                existingMetadata.VideoCodec = metadataDto.VideoCodec;
                existingMetadata.AudioCodec = metadataDto.AudioCodec;
                existingMetadata.Width = metadataDto.Width;
                existingMetadata.Height = metadataDto.Height;
                existingMetadata.HasAudio = metadataDto.HasAudio;
                existingMetadata.Title = metadataDto.Title;
                existingMetadata.Artist = metadataDto.Artist;
                existingMetadata.Album = metadataDto.Album;
                existingMetadata.Year = metadataDto.Year;
                existingMetadata.Genre = metadataDto.Genre;
                // existingMetadata.UpdatedBy = "System";

                await unitOfWork.MediaMetadata.UpdateAsync(existingMetadata, ct);
            }
            else
            {
                LogCreatingMediaMetadata(logger, fileId);

                var mediaMetadata = metadataDto.ToEntity(fileId, $"{bucketName}/{thumbnailKey}");
                mediaMetadata.ThumbnailPath = thumbnailKey;
                await unitOfWork.MediaMetadata.CreateAsync(mediaMetadata, ct);
            }

            var existingPreview = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            if (existingPreview != null)
            {
                LogUpdatingPreviewRecord(logger, existingPreview.Id);

                existingPreview.Size = new BigInteger(previewStream.Length);
                // existingPreview.UpdatedBy = "System";

                await unitOfWork.Previews.UpdateAsync(existingPreview, ct);
            }
            else
            {
                LogCreatingPreviewForMedia(logger, fileId);

                var fileEntity = new Preview
                {
                    Id = Guid.NewGuid(),
                    Name = previewKey,
                    Path = filePath,
                    MimeType = metadataDto.FormatName ?? "mp4",
                    Size = new BigInteger(previewStream.Length),
                    // UpdatedBy = "System",
                    FileId = fileId
                };

                await unitOfWork.Previews.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            LogMediaDataUploadCompleted(logger, fileId, previewStream.Length);
        }
        catch (Exception e)
        {
            LogMediaDataUploadFailed(logger, e, fileId, previewKey, thumbnailKey);

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
        }
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

    public async Task<PreviewResultDto?> GetCachedPreview(Guid id, CancellationToken ct)
    {
        LogRetrievingCachedPreview(logger, id);

        var fileData = await unitOfWork.Files.GetFileWithPreviewAsync(id, ct);

        if (fileData is null or { HasPreview: false })
        {
            LogNoPreviewAvailable(logger, id);
            return null;
        }

        var currentVersion = await unitOfWork.FileVersions.GetByIdAsync(fileData.CurrentVersionId ?? Guid.Empty, ct);
        if (currentVersion is null) throw new InvalidOperationException("File does not have a current version");
        var category = CategorizeFile(fileData.MimeType);

        LogFileCategorized(logger, category, id, fileData.MimeType);

        try
        {
            var serverHash = Convert.ToHexStringLower(currentVersion.ContentHash);

            var previewUrl = GetPreviewPresignedUrl(serverHash, TimeSpan.FromMinutes(15));

            // TODO: This is kind of not needed anymore since preview generation fixes the different type handling
            switch (category)
            {
                case FileCategory.Image:
                case FileCategory.Document:
                case FileCategory.Spreadsheet:
                case FileCategory.Presentation:
                case FileCategory.Pdf:
                    return new PreviewResultDto(new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, true),
                        previewUrl, null);
                case FileCategory.Text:
                case FileCategory.Archive:
                    break;
                case FileCategory.Audio:
                    var thumbnailUrl = GetThumbnailPresignedUrl(serverHash, TimeSpan.FromMinutes(15));
                    return new PreviewResultDto(new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, true),
                        previewUrl, thumbnailUrl);

                case FileCategory.Video:
                case FileCategory.Unknown:
                default:
                    return new PreviewResultDto(new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, true),
                        previewUrl, null);
            }

            return null;
        }
        catch (Exception ex)
        {
            LogFailedToRetrieveCachedPreview(logger, ex, id, category);
            throw;
        }
    }

    public string GetPreviewPresignedUrl(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.PreviewBucket,
            Key = $"previews/{objectKey}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            //TODO: Change this dynamically based on the environment, should be HTTPS for prod
            Protocol = Protocol.HTTP
        };

        return s3.GetPreSignedURL(request);
    }

    private string GetThumbnailPresignedUrl(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.PreviewBucket,
            Key = $"thumbnails/{objectKey}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            //TODO: Change this dynamically based on the environment, should be HTTPS for prod
            Protocol = Protocol.HTTP
        };

        return s3.GetPreSignedURL(request);
    }

    public async Task<string> GetFilePresignedUrl(Guid fileId, byte[] hash, string fileName, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.UploadBucket,
            Key = $"content/{Convert.ToHexStringLower(hash)}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTP,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            }
        };

        if (await unitOfWork.Files.IsPromoted(fileId)) return await s3.GetPreSignedURLAsync(request);

        var upload =
            await unitOfWork.Uploads.FirstOrDefaultAsync(u => u.Hash == hash && u.Status == UploadStatus.Finished) ??
            throw new InvalidOperationException("Upload hasn't finished yet");
        request.BucketName = config.Value.TempBucket;
        request.Key = $"content/{upload.TempObjectKey}";

        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<Stream> DownloadFile(Guid fileId, Guid userId, CancellationToken ct)
    {
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId && f.OwnerId == userId, ct);

        if (!fileExists)
        {
            LogFileNotFoundForDownload(logger, fileId);
            throw new InvalidOperationException($"File {fileId} not found in database.");
        }

        var hashString = await unitOfWork.Files.GetFileHashAsString(fileId, userId, ct);
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

    public async Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId && f.OwnerId == userId, ct);

        if (!fileExists)
        {
            LogFileNotFoundForDownload(logger, fileId);
            throw new InvalidOperationException($"File {fileId} not found in database.");
        }

        var hashString = await unitOfWork.Files.GetFileHashAsString(fileId, userId, ct);
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

    public async Task StreamFile(string fileId, Stream destination, CancellationToken ct)
    {
        LogStreamingFileToDestination(logger, fileId);

        var id = Guid.Parse(fileId);
        var fileData = await unitOfWork.Files.GetByIdAsync(id, ct);

        if (fileData is null)
        {
            LogFileNotFoundForStreaming(logger, fileId);
            throw new InvalidOperationException($"File with ID: {fileId} not found.");
        }

        var hash = await unitOfWork.Files.GetFileHashAsString(id, fileData.OwnerId, ct);
        try
        {
            using var response = await s3.GetObjectAsync(config.Value.UploadBucket, $"content/{hash}", ct);

            LogStreamingFileContent(logger, fileId, fileData.Name, response.ContentLength);

            await response.ResponseStream.CopyToAsync(destination, 81920, ct);

            LogFileStreamingCompleted(logger, fileId, fileData.Name);
        }
        catch (AmazonS3Exception ex)
        {
            LogS3ErrorDuringStreaming(logger, ex, fileId, fileData.Name, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToStreamFile(logger, ex, fileId, fileData.Name);
            throw;
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

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await fileService.FolderWithOwnershipExists(directoryId, userId, ct);

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

    public async Task<UploadResult> FinalizeFileUpload(
        string objectName,
        Guid uploadId,
        Guid uploadedBy,
        Guid? directoryId = null,
        CancellationToken ct = default)
    {
        if (config.Value.TempBucket is null)
            throw new InvalidOperationException("Temp bucket not configured");

        var tmp = config.Value.TempBucket;

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            await fileService.FolderWithOwnershipExists(directoryId, uploadedBy, ct);

            var upload = await unitOfWork.Uploads.GetByIdAsync(uploadId);
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
            var contentObject = await unitOfWork.ContentObjects.HashExists(computedHash);

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
                    contentObject.Id,
                    ct);

                await unitOfWork.CommitAsync(ct);

                return await CleanupAndReturnResultAsync(
                    tmp,
                    upload.TempObjectKey,
                    serverHash,
                    computedSize,
                    result.file.Id,
                    ct);
            }

            // =====================================================
            // 7. Create new version
            // =====================================================
            var currentVersion = await unitOfWork.FileVersions.GetByIdAsync(
                file.CurrentVersionId ?? Guid.Empty, ct);

            if (currentVersion is null)
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
                    FileId = file.Id
                },
                ct);

            file.CurrentVersionId = newVersion.Id;

            unitOfWork.Files.Update(file);

            await unitOfWork.CommitAsync(ct);

            await promotionQueue.QueuePromotionAsync(contentObject.Id, upload.TempObjectKey, ct);

            LogUploadFinalized(logger, file.Id, computedSize);

            return new UploadResult(
                objectName,
                serverHash,
                computedSize,
                file.Id);
        }
        catch (Exception ex)
        {
            LogFinalizeUploadFailed(logger, ex, objectName);

            await unitOfWork.RollbackAsync(ct);

            await CleanupTempObjectAsync(tmp, objectName, ct);

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


    public async Task<string> GenerateUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.TempBucket,
            Key = $"{objectKey}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTP,
            ContentType = contentType,
        };

        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.ImagesBucket,
            Key = $"{objectKey}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTP,
        };

        return await s3.GetPreSignedURLAsync(request);
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
            Protocol = Protocol.HTTP,
        };
        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<StorageBreakdown> GetStorageBreakdown(Guid userId, CancellationToken ct = default)
    {
        var trashSize = await unitOfWork.Files.GetDeletedSize(userId, ct);
        var sizeByMimeType = await unitOfWork.Files.GetSizeByType(userId, ct);
        var oldFiles = await unitOfWork.Files.GetOldFiles(userId, ct);

        return new StorageBreakdown
        {
            OldFiles = oldFiles,
            SizeByType = sizeByMimeType,
            TrashSize = trashSize
        };
    }

    public async Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default)
    {
        var hash = await unitOfWork.FileVersions.GetContentHashByVersionId(fileVersionId, ownerId, ct) ??
                   throw new InvalidOperationException("Version not found");

        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.UploadBucket,
            Key = $"content/{Convert.ToHexStringLower(hash)}",
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Protocol = Protocol.HTTP,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"Placeholder\""
            }
        };

        return await s3.GetPreSignedURLAsync(request);
    }
}