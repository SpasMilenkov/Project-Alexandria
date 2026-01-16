using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Common;
using Common.Config;
using Common.Services;
using Cppl.Utilities.AWS;
using DTO.Extensions;
using DTO.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Enumerators;
using Npgsql;
using File = Models.File;
using MediaMetadata = DTO.Files.MediaMetadata;

namespace Storage;

public class S3StorageService(
    IAmazonS3 s3,
    IUnitOfWork unitOfWork,
    IOptions<S3Config> config,
    IDirectoryService dirService,
    ILogger<S3StorageService> logger)
    : IStorageService
{
    public async Task<UploadResult> UploadFile(
        string bucketName,
        string objectName,
        string contentType,
        string clientSha256,
        Stream fileStream,
        Guid uploadedBy,
        CancellationToken ct = default,
        long contentLength = -1,
        Guid? directoryId = null,
        string? originalFileName = null
    )
    {
        logger.LogInformation(
            "Starting file upload: Bucket={BucketName}, Object={ObjectName}, ContentType={ContentType}, Size={ContentLength}",
            bucketName, objectName, contentType, contentLength);

        if (config.Value.TempBucket is null) throw new InvalidOperationException();
        var tmp = config.Value.TempBucket;
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await FolderWithOwnershipExists(directoryId, uploadedBy, ct);

            using var sha256 = SHA256.Create();
            await using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Read);

            var fileTransferUtility = new TransferUtility(s3);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = tmp,
                Key = objectName,
                InputStream = cryptoStream,
                ContentType = contentType,
                AutoCloseStream = false
            }, ct);

            if (sha256.Hash is null) throw new InvalidOperationException();

            var serverHash = Convert.ToHexStringLower(sha256.Hash);

            if (serverHash != clientSha256)
                throw new InvalidOperationException("Corruption during upload, hash differs");

            var stat = await GetVersionInfo(objectName: objectName, bucketName: tmp, ct: ct);

            var contentObject = await unitOfWork.ContentObjects.HashExists(sha256.Hash);

            if (contentObject is null)
            {
                logger.LogInformation(
                    "Creating new file record: Name={FileName}", originalFileName ?? objectName);
                var newObject = new ContentObject
                {
                    Hash = sha256.Hash,
                    StorageKey = $"content/{serverHash}",
                    RefCount = 1,
                    UpdatedBy = uploadedBy,
                };

                await unitOfWork.ContentObjects.AddAsync(newObject, ct);

                var result = await CreateFileWithVersionAsync(fileName: originalFileName ?? objectName,
                    directoryId ?? Guid.Empty,
                    contentType,
                    uploadedBy,
                    stat.Size,
                    sha256.Hash,
                    newObject.Id,
                    ct);
                try
                {
                    await s3.CopyObjectAsync(new CopyObjectRequest
                    {
                        SourceBucket = tmp,
                        SourceKey = objectName,
                        DestinationBucket = bucketName,
                        DestinationKey = $"content/{serverHash}",
                        ContentType = contentType,
                        IfNoneMatch = "*",
                    }, ct);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    logger.LogInformation("Another upload has promoted object with hash {fileHash}", serverHash);
                }


                try
                {
                    await unitOfWork.CommitAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueHashViolation(ex))
                {
                    // Another transaction won the race
                    contentObject = await unitOfWork.ContentObjects.HashExists(sha256.Hash);
                    if (contentObject is null) throw new InvalidOperationException();
                    result.fileVersion.ContentObjectId = contentObject.Id;
                    await unitOfWork.CommitAsync(ct);
                }

                return await CleanupAndReturnResultAsync(tmp, objectName, serverHash, stat, result.file.Id, ct);
            }

            contentObject.RefCount += 1;

            unitOfWork.ContentObjects.Update(contentObject);

            var fileMatch = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Name == objectName &&
                                                                            f.DirectoryId == directoryId &&
                                                                            f.OwnerId == uploadedBy, ct);

            if (fileMatch is null)
            {
                var result = await CreateFileWithVersionAsync(fileName: originalFileName ?? objectName,
                    directoryId ?? Guid.Empty,
                    contentType,
                    uploadedBy,
                    stat.Size,
                    sha256.Hash,
                    contentObject.Id,
                    ct);

                await unitOfWork.CommitAsync(ct);

                return await CleanupAndReturnResultAsync(tmp, objectName, serverHash, stat, result.file.Id, ct);
            }

            logger.LogInformation(
                "Creating new file record:  Name={FileName}",
                originalFileName ?? objectName);


            var currentVersion =
                await unitOfWork.FileVersions.GetByIdAsync(fileMatch.CurrentVersionId ?? Guid.Empty, ct);
            if (currentVersion is null)
                throw new InvalidOperationException("Version misamtch");

            var newVersion = await unitOfWork.FileVersions.AddAsync(new FileVersion
            {
                ContentHash = sha256.Hash,
                Size = stat.Size,
                VersionNumber = currentVersion.VersionNumber + 1,
                MimeType = contentType,
                CreatedBy = uploadedBy,
                ContentObjectId = contentObject.Id,
                FileId = fileMatch.Id
            }, ct);
            fileMatch.CurrentVersionId = newVersion.Id;

            unitOfWork.Files.Update(fileMatch);

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "File upload completed successfully: FileId={FileId}, Size={Size}, VersionId={VersionId}",
                fileMatch.Id, stat.Size, newVersion.Id);

            return new UploadResult(
                objectName,
                serverHash,
                Guid.Parse(stat.VersionId),
                stat.Size,
                fileMatch.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File upload failed: Bucket={BucketName}, Object={ObjectName}, Error={ErrorMessage}",
                bucketName, objectName, ex.Message);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                await s3.DeleteObjectAsync(tmp, objectName, ct);

                logger.LogInformation(
                    "Cleanup successful: Object deleted from storage: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);
            }
            catch (Exception cleanupEx)
            {
                logger.LogError(cleanupEx,
                    "Failed to cleanup object after upload failure: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);
            }

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
    }

    private async Task<(File file, FileVersion fileVersion)> CreateFileWithVersionAsync(string fileName,
        Guid directoryId,
        string contentType,
        Guid uploadedBy,
        long size,
        byte[] hash,
        Guid contentObjectId,
        CancellationToken ct)
    {
        logger.LogInformation("Creating new file record: Name={FileName}", fileName);

        var fileEntity = new File
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
        VersionInfo stat, Guid fileId, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(tmpBucket, objectName, ct);
        return new UploadResult(
            objectName,
            serverHash,
            Guid.Parse(stat.VersionId),
            stat.Size,
            fileId);
    }

    private async Task FolderWithOwnershipExists(Guid? directoryId, Guid ownerId, CancellationToken ct = default)
    {
        if (directoryId is null) return;

        var exists = await dirService.DirectoryExistsWithOwnershipAsync((Guid)directoryId, ownerId, ct);
        if (!exists) throw new DirectoryNotFoundException();
    }

    public async Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        await FolderWithOwnershipExists(destinationId, userId, ct);

        await unitOfWork.Files.MoveFilesAsync(fileIds, destinationId, userId, ct);
    }

    public async Task CopyFilesAsync(Guid[] fileIds, Guid destinationId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        await FolderWithOwnershipExists(destinationId, userId, ct);

        await unitOfWork.Files.CopyFilesAsync(fileIds, destinationId, userId, ct);

        await unitOfWork.CommitAsync(ct);
    }

    private static bool IsUniqueHashViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            // PostgreSQL unique constraint violation
            return pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        return false;
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
        logger.LogInformation(
            "Starting preview upload: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}",
            bucketName, objectName, originalFileId);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var filePath = $"{bucketName}/{objectName}";

            var fileTransferUtility = new TransferUtility(s3);

            using var sha256 = SHA256.Create();
            await using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Read);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = cryptoStream,
                ContentType = contentType,
                AutoCloseStream = false
            }, ct);
            if (sha256.Hash is null)
                logger.LogWarning("Failed to calculate hash for preview of file {FileId}", originalFileId);

            var serverHash = Convert.ToHexStringLower(sha256.Hash);

            var stat = await GetVersionInfo(objectName: objectName, bucketName: bucketName, ct: ct);

            var existingFile = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            Preview savedFile;

            if (existingFile != null)
            {
                logger.LogInformation(
                    "Updating existing preview record: PreviewId={PreviewId}, Path={Path}",
                    existingFile.Id, filePath);

                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger(stat.Size);
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
                    Size = new BigInteger(stat.Size),
                    UpdatedBy = uploadedBy,
                    FileId = originalFileId
                };

                savedFile = await unitOfWork.Previews.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "Preview upload completed successfully: PreviewId={PreviewId}, Size={Size}",
                savedFile.Id, stat.Size);

            //TODO: Replace the empty guid with a proper file version UUID
            return new UploadResult(
                objectName,
                serverHash,
                Guid.Empty,
                stat.Size,
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

            var fileTransferUtility = new TransferUtility(s3);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = $"previews/{objectName}",
                InputStream = previewStream,
                ContentType = GetMimeTypeFromFormat(metadataDto.FormatName),
                AutoCloseStream = false
            }, ct);

            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;

            logger.LogDebug("Uploading thumbnail: ThumbnailKey={ThumbnailKey}", thumbnailKey);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = $"thumbnails/{objectName}",
                InputStream = thumbnailStream,
                ContentType = "image/jpeg",
                AutoCloseStream = false
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

            var stat = await GetVersionInfo(objectName: previewKey, bucketName: bucketName, ct: ct);

            var existingPreview = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            if (existingPreview != null)
            {
                logger.LogDebug(
                    "Updating existing preview record: PreviewId={PreviewId}",
                    existingPreview.Id);

                existingPreview.Size = new BigInteger(stat.Size);
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
                    Size = new BigInteger(stat.Size),
                    // UpdatedBy = "System",
                    FileId = fileId
                };

                await unitOfWork.Previews.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "Media data upload completed successfully: FileId={FileId}, PreviewSize={PreviewSize}",
                fileId, stat.Size);
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

    public async Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetByIdAsync(fileId, ct);

    public async Task<FileSummary?> GetFileSummary(Guid fieldId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetFileNameAndMimeType(fieldId, ct);

    public async Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default) =>
        await unitOfWork.Files.FindAsync(f => f.MimeType == mimeType, ct);

    public async Task DeleteFiles(Guid[] fileIds, Guid userId, bool hardDelete = false, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await unitOfWork.Files.MarkAsDeleted(fileIds, userId, ct);
            if (hardDelete)
                await unitOfWork.FileVersions.DeleteFileVersions(fileIds, userId, ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Delete failed: {ex.Message}", ex);
        }
    }

    public async Task<File> UpdateFileMetadata(
        Guid fileId,
        Guid updatedBy,
        string? newName = null,
        bool? hasPreview = null,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Updating file metadata: FileId={FileId}, NewName={NewName}, HasPreview={HasPreview}, UpdatedBy={UpdatedBy}",
            fileId, newName, hasPreview, updatedBy);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var fileEntity = await unitOfWork.Files.GetByIdAsync(fileId, ct);
            if (fileEntity == null)
            {
                logger.LogWarning("File not found for metadata update: FileId={FileId}", fileId);
                throw new InvalidOperationException($"File with ID {fileId} not found.");
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(newName))
            {
                logger.LogDebug("Updating file name: FileId={FileId}, OldName={OldName}, NewName={NewName}",
                    fileId, fileEntity.Name, newName);
                fileEntity.Name = newName;
            }

            if (hasPreview.HasValue)
            {
                logger.LogDebug("Updating preview status: FileId={FileId}, HasPreview={HasPreview}",
                    fileId, hasPreview.Value);
                fileEntity.HasPreview = hasPreview.Value;
                if (hasPreview.Value) fileEntity.PreviewGeneratedAt = DateTime.UtcNow;
            }

            fileEntity.UpdatedBy = updatedBy;

            var updatedFile = await unitOfWork.Files.UpdateAsync(fileEntity, ct);
            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "File metadata updated successfully: FileId={FileId}",
                fileId);

            return updatedFile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File metadata update failed: FileId={FileId}",
                fileId);

            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Update failed: {ex.Message}", ex);
        }
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

    public async Task<VersionInfo> GetVersionInfo(string bucketName, string objectName, CancellationToken ct)
    {
        logger.LogDebug(
            "Retrieving version info: Bucket={BucketName}, Object={ObjectName}",
            bucketName, objectName);

        try
        {
            var metadata = await s3.GetObjectMetadataAsync(bucketName, objectName, ct);

            logger.LogDebug(
                "Version info retrieved: Bucket={BucketName}, Object={ObjectName}, VersionId={VersionId}, Size={Size}",
                bucketName, objectName, metadata.VersionId, metadata.ContentLength);

            return new VersionInfo(
                metadata.VersionId ?? Guid.Empty.ToString(),
                metadata.ContentLength
            );
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error retrieving version info: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}",
                bucketName, objectName, ex.StatusCode);
            throw;
        }
    }

    public async Task<PaginatedResult<FileResult>> GetRootFilesAsync(Guid ownerId, int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        return await unitOfWork.Directories.GetRootFilesAsync(ownerId, page, pageSize, sortBy,
            sortDirection,
            ct);
    }

    public async Task<PaginatedResult<FileResult>> GetFilesByDirectoryId(Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        var directoryExists =
            await unitOfWork.Directories.ExistsAsync(d => d.Id == directoryId && d.OwnerId == ownerId);

        if (!directoryExists) throw new Directories.Exceptions.DirectoryNotFoundException(directoryId);

        return await unitOfWork.Files.GetFilesByDirectoryIdAsync(directoryId,
            ownerId,
            page, pageSize,
            sortDirection,
            sortBy,
            ct
        );
    }

    public async Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(mimeTypeFilter)) return await unitOfWork.Files.CountAsync(null, ct);

        return await unitOfWork.Files.CountAsync(f => f.MimeType == mimeTypeFilter, ct);
    }
}