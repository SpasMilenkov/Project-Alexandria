using System.Net;
using System.Numerics;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Common;
using Common.Config;
using Common.Services;
using DTO;
using DTO.Extensions;
using DTO.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Enumerators;
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
        Stream fileStream,
        CancellationToken ct = default,
        long contentLength = -1,
        Guid? directoryId = null,
        string? originalFileName = null,
        string? uploadedBy = null)
    {
        logger.LogInformation(
            "Starting file upload: Bucket={BucketName}, Object={ObjectName}, ContentType={ContentType}, Size={ContentLength}",
            bucketName, objectName, contentType, contentLength);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            if (directoryId is not null)
            {
                var exists = await dirService.DirectoryExistsAsync((Guid)directoryId, ct);
                if(!exists) throw new DirectoryNotFoundException();
            }
            
            var filePath = $"{bucketName}/{objectName}";

            await using var checksumStream = new ChecksumCalculatingStream(fileStream);

            var fileTransferUtility = new TransferUtility(s3);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = fileStream,
                ContentType = contentType,
                AutoCloseStream = false,
            }, ct);

            // var response = await s3.PutObjectAsync(putRequest, ct);

            var calculatedChecksum = checksumStream.GetChecksum();

            var stat = await GetVersionInfo(objectName: objectName, bucketName: bucketName, ct: ct);

            var existingFile = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            File savedFile;

            if (existingFile != null)
            {
                logger.LogInformation(
                    "Updating existing file record: FileId={FileId}, Path={Path}",
                    existingFile.Id, filePath);

                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger(stat.Size);
                existingFile.UpdatedBy = uploadedBy;
                existingFile.DirectoryId = directoryId;
                
                savedFile = await unitOfWork.Files.UpdateAsync(existingFile, ct);
            }
            else
            {
                logger.LogInformation(
                    "Creating new file record: Path={Path}, Name={FileName}",
                    filePath, originalFileName ?? objectName);

                var fileEntity = new File
                {
                    Id = Guid.NewGuid(),
                    Name = originalFileName ?? objectName,
                    DirectoryId = directoryId,
                    Path = filePath,
                    MimeType = contentType,
                    Size = new BigInteger(stat.Size),
                    UpdatedBy = uploadedBy
                };

                savedFile = await unitOfWork.Files.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "File upload completed successfully: FileId={FileId}, Path={Path}, Size={Size}, VersionId={VersionId}",
                savedFile.Id, filePath, stat.Size, stat.VersionId);

            return new UploadResult(
                objectName,
                filePath,
                calculatedChecksum,
                stat.VersionId,
                stat.Size,
                savedFile.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File upload failed: Bucket={BucketName}, Object={ObjectName}, Error={ErrorMessage}",
                bucketName, objectName, ex.Message);

            await unitOfWork.RollbackAsync(ct);

            try
            {
                logger.LogWarning(
                    "Attempting cleanup: Deleting object from storage: Bucket={BucketName}, Object={ObjectName}",
                    bucketName, objectName);

                await s3.DeleteObjectAsync(bucketName, objectName, ct);

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

    public async Task<UploadResult> UploadPreview(
        string bucketName,
        string objectName,
        string contentType,
        Stream fileStream,
        Guid originalFileId,
        long contentLength = -1,
        string? originalFileName = null,
        string? uploadedBy = null,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Starting preview upload: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}",
            bucketName, objectName, originalFileId);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var filePath = $"{bucketName}/{objectName}";

            await using var checksumStream = new ChecksumCalculatingStream(fileStream);

            var fileTransferUtility = new TransferUtility(s3);

            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = fileStream,
                ContentType = contentType,
                AutoCloseStream = false,
            }, ct);
            
            var calculatedChecksum = checksumStream.GetChecksum();

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

            return new UploadResult(
                objectName,
                filePath,
                calculatedChecksum,
                stat.VersionId,
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
                AutoCloseStream = false,
            }, ct);
            
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;

            logger.LogDebug("Uploading thumbnail: ThumbnailKey={ThumbnailKey}", thumbnailKey);
            
            await fileTransferUtility.UploadAsync(new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = thumbnailStream,
                ContentType = "image/jpeg",
                AutoCloseStream = false,
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
                existingMetadata.UpdatedBy = "System";

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
                existingPreview.UpdatedBy = "System";

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
                    UpdatedBy = "System",
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
                "application/x-rar-compressed" or "application/gzip" or "application/x-tar"
                => FileCategory.Archive,

            // ====== Text (Markdown, JSON, HTML, XML, etc.) ======
            "text/markdown" or "application/json" or "application/xml" or "text/xml" or "text/html" or "text/plain"
                => FileCategory.Text,

            // ====== Fallback ======
            _ => FileCategory.Unknown
        };
    }

    public async Task<FileResultSummary?> GetCachedPreview(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Retrieving cached preview: FileId={FileId}", id);

        var fileData = await unitOfWork.Files.GetFileWithPreviewAsync(id, ct);

        if (fileData.Preview is null)
        {
            logger.LogDebug("No preview available for file: FileId={FileId}", id);
            return null;
        }

        var category = CategorizeFile(fileData.MimeType);

        logger.LogDebug(
            "File categorized as {Category} for preview: FileId={FileId}, MimeType={MimeType}",
            category, id, fileData.MimeType);

        try
        {
            switch (category)
            {
                case FileCategory.Image:
                    return new FileResultSummary(
                        await DownloadFileNoCheckAsync(config.Value.PreviewBucket ?? "user-previews",
                            "previews/" + fileData.Name, ct), new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, true,
                            fileData.Preview.Path));
                case FileCategory.Document:
                case FileCategory.Spreadsheet:
                case FileCategory.Presentation:
                case FileCategory.Pdf:
                    return new FileResultSummary(
                        await DownloadFileNoCheckAsync(config.Value.PreviewBucket ?? "user-previews",
                            "previews/" + fileData.Name + ".pdf", ct),
                        new FileSummary(fileData.Id, fileData.Name, fileData.Preview.MimeType, true,
                            fileData.Preview.Path));
                case FileCategory.Text:
                    break;
                case FileCategory.Archive:
                case FileCategory.Audio:
                case FileCategory.Video:
                case FileCategory.Unknown:
                default:
                    return new FileResultSummary(
                        await DownloadFileNoCheckAsync(config.Value.PreviewBucket ?? "user-previews",
                            "previews/" + fileData.Name, ct), new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, true,
                            fileData.Preview.Path));
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

    public async Task<FileResult> GetFileById(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Retrieving file by ID: FileId={FileId}", id);

        var fileData = await GetFileMetadata(id, ct);
        if (fileData is null)
        {
            logger.LogWarning("File metadata not found: FileId={FileId}", id);
            throw new FileNotFoundException($"File with ID {id} not found");
        }

        //TODO: Remove magic number later, should be 20MB in bytes
        if (fileData.Size > 40971520)
        {
            logger.LogWarning(
                "File size exceeds preview limit: FileId={FileId}, Size={FileSize}",
                id, fileData.Size);
            throw new InvalidOperationException("Filesize too large for preview");
        }

        logger.LogInformation(
            "Retrieving file content: FileId={FileId}, Name={FileName}, Size={FileSize}",
            id, fileData.Name, fileData.Size);

        return new FileResult(await DownloadFile(config.Value.UploadBucket ?? "user-uploads", fileData.Name, ct),
            fileData);
    }

    public async Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetByIdAsync(fileId, ct);
    }

    public async Task<FileSummary?> GetFileSummary(Guid fieldId, CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetFileNameAndMimeType(fieldId, ct);
    }

    public async Task<File?> GetFileByPath(string path, CancellationToken ct = default)
    {
        return await unitOfWork.Files.FirstOrDefaultAsync(f => f.Path == path, ct);
    }

    public async Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default)
    {
        return await unitOfWork.Files.FindAsync(f => f.MimeType == mimeType, ct);
    }

    public async Task DeleteFile(string bucketName, string objectName, CancellationToken ct, bool hardDelete = false)
    {
        logger.LogInformation(
            "Deleting file: Bucket={BucketName}, Object={ObjectName}, HardDelete={HardDelete}",
            bucketName, objectName, hardDelete);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var path = $"{bucketName}/{objectName}";
            var fileEntity = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Path == path, ct);

            if (fileEntity == null)
            {
                logger.LogWarning(
                    "File not found in database for deletion: Path={Path}",
                    path);
                throw new InvalidOperationException($"File with path {path} not found in database.");
            }

            if (hardDelete)
            {
                logger.LogInformation(
                    "Performing hard delete: FileId={FileId}, Path={Path}",
                    fileEntity.Id, path);

                // Remove from S3
                await s3.DeleteObjectAsync(bucketName, objectName, ct);

                logger.LogDebug("Object deleted from storage: Path={Path}", path);

                // Hard delete from database
                unitOfWork.Files.Remove(fileEntity);
            }
            else
            {
                logger.LogInformation(
                    "Performing soft delete: FileId={FileId}, Path={Path}",
                    fileEntity.Id, path);

                // Soft delete 
                unitOfWork.Files.Remove(fileEntity);
            }

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "File deletion completed successfully: FileId={FileId}, HardDelete={HardDelete}",
                fileEntity.Id, hardDelete);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File deletion failed: Bucket={BucketName}, Object={ObjectName}, HardDelete={HardDelete}",
                bucketName, objectName, hardDelete);

            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Delete failed: {ex.Message}", ex);
        }
    }

    public async Task<File> UpdateFileMetadata(
        Guid fileId,
        string? newName = null,
        bool? hasPreview = null,
        string? updatedBy = null,
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

            if (!string.IsNullOrEmpty(updatedBy)) fileEntity.UpdatedBy = updatedBy;

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

    public async Task<Stream> DownloadFile(string? bucketName, string objectName, CancellationToken ct)
    {
        bucketName ??= config.Value.UploadBucket;

        logger.LogDebug(
            "Downloading file with validation: Bucket={BucketName}, Object={ObjectName}",
            bucketName, objectName);

        var path = $"{bucketName}/{objectName}";
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Path == path, ct);

        if (!fileExists)
        {
            logger.LogWarning(
                "File not found in database during download: Path={Path}",
                path);
            throw new InvalidOperationException($"File {path} not found in database.");
        }

        try
        {
            var response = await s3.GetObjectAsync(bucketName, objectName, ct);

            logger.LogInformation(
                "File download stream acquired: Bucket={BucketName}, Object={ObjectName}, Size={ContentLength}",
                bucketName, objectName, response.ContentLength);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error during file download: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}",
                bucketName, objectName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to download file: Bucket={BucketName}, Object={ObjectName}",
                bucketName, objectName);
            throw;
        }
    }

    private async Task<Stream> DownloadFileNoCheckAsync(string? bucketName, string objectName, CancellationToken ct)
    {
        bucketName ??= config.Value.UploadBucket;

        logger.LogDebug(
            "Downloading file without validation: Bucket={BucketName}, Object={ObjectName}",
            bucketName, objectName);

        try
        {
            var response = await s3.GetObjectAsync(bucketName, objectName, ct);

            logger.LogDebug(
                "File download stream acquired (no check): Bucket={BucketName}, Object={ObjectName}",
                bucketName, objectName);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex,
                "S3 error during unchecked file download: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}",
                bucketName, objectName, ex.StatusCode);
            throw;
        }
    }

    public async Task StreamFile(string fileId, Stream destination, CancellationToken ct)
    {
        logger.LogInformation(
            "Streaming file to destination: FileId={FileId}",
            fileId);

        var fileData = await unitOfWork.Files.GetByIdAsync(Guid.Parse(fileId), ct);

        if (fileData is null)
        {
            logger.LogWarning(
                "File not found for streaming: FileId={FileId}",
                fileId);
            throw new InvalidOperationException($"File with ID: {fileId} not found.");
        }

        try
        {
            using var response = await s3.GetObjectAsync(config.Value.UploadBucket, fileData.Name, ct);

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

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        logger.LogInformation("Ensuring bucket exists: BucketName={BucketName}", bucketName);

        try
        {
            // Check if bucket exists
            var bucketsResponse = await s3.ListBucketsAsync(ct);
            var bucketExists = bucketsResponse.Buckets.Any(b => b.BucketName == bucketName);

            if (!bucketExists)
            {
                logger.LogInformation("Bucket does not exist, creating: BucketName={BucketName}", bucketName);

                // Create bucket
                await s3.PutBucketAsync(bucketName, ct);

                logger.LogInformation("Bucket created successfully: BucketName={BucketName}", bucketName);
            }
            else
            {
                logger.LogDebug("Bucket already exists: BucketName={BucketName}", bucketName);
            }

            // Enable versioning
            logger.LogDebug("Enabling versioning for bucket: BucketName={BucketName}", bucketName);

            var versioningRequest = new PutBucketVersioningRequest
            {
                BucketName = bucketName,
                VersioningConfig = new S3BucketVersioningConfig
                {
                    Status = VersionStatus.Enabled
                }
            };
            await s3.PutBucketVersioningAsync(versioningRequest, ct);

            logger.LogInformation("Bucket versioning enabled: BucketName={BucketName}", bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogInformation(
                "Bucket already exists (conflict): BucketName={BucketName}",
                bucketName);
            // Bucket already exists - this is fine
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to ensure bucket exists: BucketName={BucketName}",
                bucketName);
            throw;
        }
    }

    public Task DeleteFile(string bucketName, string objectName, CancellationToken ct)
    {
        logger.LogInformation(
            "Deleting object from storage: Bucket={BucketName}, Object={ObjectName}",
            bucketName, objectName);

        try
        {
            return s3.DeleteObjectAsync(bucketName, objectName, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to delete object: Bucket={BucketName}, Object={ObjectName}",
                bucketName, objectName);
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
                metadata.VersionId ?? string.Empty,
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

    public async Task<IEnumerable<File>> GetAllFiles(CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetAllAsync(ct);
    }

    public async Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(mimeTypeFilter)) return await unitOfWork.Files.CountAsync(null, ct);

        return await unitOfWork.Files.CountAsync(f => f.MimeType == mimeTypeFilter, ct);
    }
}