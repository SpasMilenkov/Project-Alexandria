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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Enumerators;
using MediaMetadata = DTO.Files.MediaMetadata;
using FileEntity = Models.File;
using DTO.Metrics;

namespace Storage;

public class S3Service(
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
            logger.LogDebug("Cleaned up temp object: {Bucket}/{Key}", bucket, key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Object already deleted or never existed
            logger.LogDebug("Temp object not found (already deleted): {Bucket}/{Key}", bucket, key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to cleanup temp object: {Bucket}/{Key}", bucket, key);
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
        logger.LogInformation("Creating new file record: Name={FileName}", fileName);

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
        long contentLength,
        string? originalFileName = null,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Starting preview upload: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}",
            bucketName, objectName, originalFileId);

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
                logger.LogInformation(
                    "Updating existing preview record: PreviewId={PreviewId}, Path={Path}",
                    existingFile.Id, filePath);

                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger();
                existingFile.UpdatedBy = uploadedBy;

                savedFile = await unitOfWork.Previews.UpdateAsync(existingFile, ct);
            }
            else
            {
                logger.LogInformation(
                    "Creating new preview record: Path={Path}, OriginalFileId={OriginalFileId}",
                    filePath, originalFileId);

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

            logger.LogInformation(
                "Preview upload completed successfully: PreviewId={PreviewId}, Size={Size}",
                savedFile.Id, contentLength);

            return new UploadResult(
                objectName,
                "",
                contentLength,
                savedFile.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Preview upload failed: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}",
                bucketName, objectName, originalFileId);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                logger.LogWarning(
                    "Attempting cleanup: Deleting preview from storage: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);

                await s3.DeleteObjectAsync(bucketName, objectName, ct);

                logger.LogInformation(
                    "Cleanup successful: Preview deleted from storage: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx,
                    "Failed to cleanup preview after upload failure: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);
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

        logger.LogInformation(
            "Starting media data upload: FileId={FileId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}",
            fileId, previewKey, thumbnailKey);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var originalFileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId, ct);

            if (!originalFileExists)
            {
                logger.LogError(
                    "Original file not found for media data upload: FileId={FileId}",
                    fileId);
                throw new InvalidOperationException($"Original file with ID {fileId} not found");
            }

            if (previewStream.CanSeek) previewStream.Position = 0;
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;

            logger.LogDebug("Uploading preview video: PreviewKey={PreviewKey}", previewKey);

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

            logger.LogDebug("Uploading thumbnail: ThumbnailKey={ThumbnailKey}", thumbnailKey);

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
                logger.LogInformation(
                    "Updating existing media metadata: MetadataId={MetadataId}, FileId={FileId}",
                    existingMetadata.Id, fileId);

                // Update existing metadata
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
                logger.LogInformation(
                    "Creating new media metadata record: FileId={FileId}",
                    fileId);

                var mediaMetadata = metadataDto.ToEntity(fileId, $"{bucketName}/{thumbnailKey}");
                mediaMetadata.ThumbnailPath = thumbnailKey;
                await unitOfWork.MediaMetadata.CreateAsync(mediaMetadata, ct);
            }

            var existingPreview = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            if (existingPreview != null)
            {
                logger.LogDebug(
                    "Updating existing preview record: PreviewId={PreviewId}",
                    existingPreview.Id);

                existingPreview.Size = new BigInteger(previewStream.Length);
                // existingPreview.UpdatedBy = "System";

                await unitOfWork.Previews.UpdateAsync(existingPreview, ct);
            }
            else
            {
                logger.LogDebug(
                    "Creating new preview record for media: FileId={FileId}",
                    fileId);

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

            logger.LogInformation(
                "Media data upload completed successfully: FileId={FileId}, PreviewSize={PreviewSize}",
                fileId, previewStream.Length);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Failed to upload media data: FileId={FileId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}",
                fileId, previewKey, thumbnailKey);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                logger.LogWarning(
                    "Attempting cleanup of media data: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}",
                    previewKey, thumbnailKey);

                await s3.DeleteObjectAsync(bucketName, previewKey, ct);
                await s3.DeleteObjectAsync(bucketName, thumbnailKey, ct);

                logger.LogInformation(
                    "Media data cleanup successful: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}",
                    previewKey, thumbnailKey);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx,
                    "Failed to clean up media data after upload failure: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}",
                    previewKey, thumbnailKey);
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

    public async Task<PreviewResultDto?> GetCachedPreview(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Retrieving cached preview: FileId={FileId}", id);

        var fileData = await unitOfWork.Files.GetFileWithPreviewAsync(id, ct);

        if (fileData is null or { HasPreview: false })
        {
            logger.LogDebug("No preview available for file: FileId={FileId}", id);
            return null;
        }

        var currentVersion = await unitOfWork.FileVersions.GetByIdAsync(fileData.CurrentVersionId ?? Guid.Empty, ct);
        if (currentVersion is null) throw new InvalidOperationException("File does not have a current version");
        var category = CategorizeFile(fileData.MimeType);

        logger.LogDebug(
            "File categorized as {Category} for preview: FileId={FileId}, MimeType={MimeType}",
            category, id, fileData.MimeType);

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
            logger.LogError(ex,
                "Failed to retrieve cached preview: FileId={FileId}, Category={Category}",
                id, category);
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
            await unitOfWork.Uploads.FirstOrDefaultAsync(u => u.Hash == hash && u.Status == UploadStatus.Finished);
        if (upload is null) throw new InvalidObjectStateException();

        request.BucketName = config.Value.TempBucket;
        request.Key = $"content/{upload.TempObjectKey}";

        return await s3.GetPreSignedURLAsync(request);
    }

    public async Task<Stream> DownloadFile(Guid fileId, Guid userId, CancellationToken ct)
    {
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId && f.OwnerId == userId, ct);

        if (!fileExists)
        {
            logger.LogWarning(
                "File not found in database during download: Id={fileId}",
                fileId);
            throw new InvalidOperationException($"File {fileId} not found in database.");
        }

        var hashString = await unitOfWork.Files.GetFileHashAsString(fileId, userId, ct);
        try
        {
            var response = await s3.GetObjectAsync(config.Value.UploadBucket, $"content/{hashString}", ct);

            logger.LogInformation(
                "File download stream acquired: Bucket={BucketName}, Object={ObjectName}, Size={ContentLength}",
                config.Value.UploadBucket, $"content/{hashString}", response.ContentLength);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error during file download: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}",
                config.Value.UploadBucket, $"content/{hashString}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to download file: Bucket={BucketName}, Object={ObjectName}",
                config.Value.UploadBucket, $"content/{hashString}");
            throw;
        }
    }

    public async Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct)
    {
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId && f.OwnerId == userId, ct);

        if (!fileExists)
        {
            logger.LogWarning(
                "File not found in database during download: Id={fileId}",
                fileId);
            throw new InvalidOperationException($"File {fileId} not found in database.");
        }

        var hashString = await unitOfWork.Files.GetFileHashAsString(fileId, userId, ct);
        try
        {
            var stream =
                new SeekableS3Stream(s3, config.Value.UploadBucket, $"content/{hashString}", 128 * 1024, 12);

            logger.LogInformation(
                "File download stream acquired: Bucket={BucketName}, Object={ObjectName}, Size={ContentLength}",
                config.Value.UploadBucket, $"content/{hashString}", stream.Length);

            return stream;
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error during file download: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}",
                config.Value.UploadBucket, $"content/{hashString}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to download file: Bucket={BucketName}, Object={ObjectName}",
                config.Value.UploadBucket, $"content/{hashString}");
            throw;
        }
    }

    public async Task StreamFile(string fileId, Stream destination, CancellationToken ct)
    {
        logger.LogInformation(
            "Streaming file to destination: FileId={FileId}",
            fileId);
        var id = Guid.Parse(fileId);
        var fileData = await unitOfWork.Files.GetByIdAsync(id, ct);

        if (fileData is null)
        {
            logger.LogWarning(
                "File not found for streaming: FileId={FileId}",
                fileId);
            throw new InvalidOperationException($"File with ID: {fileId} not found.");
        }

        var hash = await unitOfWork.Files.GetFileHashAsString(id, fileData.OwnerId, ct);
        try
        {
            using var response = await s3.GetObjectAsync(config.Value.UploadBucket, $"content/{hash}", ct);

            logger.LogDebug(
                "Streaming file content: FileId={FileId}, Name={FileName}, Size={ContentLength}",
                fileId, fileData.Name, response.ContentLength);

            await response.ResponseStream.CopyToAsync(destination, 81920, ct);

            logger.LogInformation(
                "File streaming completed: FileId={FileId}, Name={FileName}",
                fileId, fileData.Name);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error during file streaming: FileId={FileId}, Name={FileName}, StatusCode={StatusCode}",
                fileId, fileData.Name, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to stream file: FileId={FileId}, Name={FileName}",
                fileId, fileData.Name);
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
            /*
             *  TODO: Here instead of doing this I can implement the proof of knowledge with a merkle tree
                in the future this can save the bandwidth of the user with true deduplication
             */
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

            return (upload.Id, await GenerateUploadUrl(tempObjectKey, contentType, TimeSpan.FromSeconds(60)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File upload failed: Bucket={BucketName}, Object={ObjectName}, Error={ErrorMessage}",
                tmp, tempObjectKey, ex.Message);

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
            if (!CryptographicOperations.FixedTimeEquals(
                    computedHash,
                    upload.Hash))
            {
                await CleanupTempObjectAsync(tmp, upload.TempObjectKey, ct);

                upload.Status = UploadStatus.Interrupted;
                upload.FinishedAt = DateTime.UtcNow;

                throw new InvalidOperationException(
                    "SHA256 mismatch between upload and server");
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
            var contentObject =
                await unitOfWork.ContentObjects.HashExists(computedHash);

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
            var file =
                await unitOfWork.Files.FirstOrDefaultAsync(
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
            var currentVersion =
                await unitOfWork.FileVersions.GetByIdAsync(
                    file.CurrentVersionId ?? Guid.Empty,
                    ct);

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

            logger.LogInformation(
                "Upload finalized: FileId={FileId}, Size={Size}",
                file.Id,
                computedSize);

            return new UploadResult(
                objectName,
                serverHash,
                computedSize,
                file.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Finalize upload failed: {Object}",
                objectName);

            await unitOfWork.RollbackAsync(ct);

            await CleanupTempObjectAsync(
                tmp,
                objectName,
                ct);

            throw new InvalidOperationException(
                $"Upload failed: {ex.Message}", ex);
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

        // 32 bytes by default (matches your DB constraint)
        Hash hash = hasher.Finalize();
        byte[] hashBytes = hash.AsSpan().ToArray();

        return (hashBytes, totalBytes);
    }


    public async Task<string> GenerateUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.Value.TempBucket,
            Key = $"content/{objectKey}",
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = Protocol.HTTP,
            ContentType = contentType,
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
}
