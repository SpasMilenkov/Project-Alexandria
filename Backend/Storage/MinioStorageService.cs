using System.Numerics;
using Common;
using Common.Services;
using DTO;
using DTO.Extensions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Models;
using Models.Enumerators;
using File = Models.File;
using MediaMetadata = DTO.MediaMetadata;
using MinioConfig = Common.Config.MinioConfig;

namespace Storage;

public class MinioStorageService(
    IMinioClient minio,
    IUnitOfWork unitOfWork,
    IOptions<MinioConfig> config)
    : IStorageService
{
    public async Task<UploadResult> UploadFile(
        string bucketName,
        string objectName,
        string contentType,
        Stream fileStream,
        CancellationToken ct,
        long contentLength,
        string? originalFileName = null,
        string? uploadedBy = null)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var filePath = $"{bucketName}/{objectName}";

            await using var checksumStream = new ChecksumCalculatingStream(fileStream);

            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(checksumStream)
                .WithObjectSize(contentLength > 0 ? contentLength : -1)
                .WithContentType(contentType), ct);

            var calculatedChecksum = checksumStream.GetChecksum();

            var stat = await GetVersionInfo(objectName: objectName, bucketName: bucketName, ct: ct);

            var existingFile = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            File savedFile;

            if (existingFile != null)
            {
                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger(stat.Size);
                existingFile.UpdatedBy = uploadedBy;

                savedFile = await unitOfWork.Files.UpdateAsync(existingFile, ct);
            }
            else
            {
                var fileEntity = new File
                {
                    Id = Guid.NewGuid(),
                    Name = originalFileName ?? objectName,
                    Path = filePath,
                    MimeType = contentType,
                    Size = new BigInteger(stat.Size),
                    UpdatedBy = uploadedBy
                };

                savedFile = await unitOfWork.Files.CreateAsync(fileEntity, ct);
            }

            await unitOfWork.CommitAsync(ct);

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
            await unitOfWork.RollbackAsync(ct);

            try
            {
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);
            }
            catch
            {
                // TODO: Log errors with proper logging
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
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var filePath = $"{bucketName}/{objectName}";

            await using var checksumStream = new ChecksumCalculatingStream(fileStream);

            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(checksumStream)
                .WithObjectSize(contentLength > 0 ? contentLength : -1)
                .WithContentType(contentType), ct);

            var calculatedChecksum = checksumStream.GetChecksum();

            var stat = await GetVersionInfo(objectName: objectName, bucketName: bucketName, ct: ct);

            var existingFile = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            Preview savedFile;

            if (existingFile != null)
            {
                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger(stat.Size);
                existingFile.UpdatedBy = uploadedBy;

                savedFile = await unitOfWork.Previews.UpdateAsync(existingFile, ct);
            }
            else
            {
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
            await unitOfWork.RollbackAsync(ct);

            try
            {
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);
            }
            catch
            {
                // TODO: Log errors with proper logging
            }

            throw new InvalidOperationException($"Upload failed: {ex.Message}", ex);
        }
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
        var bucketName = config.Value.PreviewBucket;
        var filePath = $"{bucketName}/{objectName}";
        var previewKey = $"previews/{objectName}";
        var thumbnailKey = $"thumbnails/{fileId}.jpg";

        await unitOfWork.BeginTransactionAsync(ct);
        
        try
        {
            var originalFileExists = await unitOfWork.Files.ExistsAsync(f => f.Id == fileId, ct);

            if (!originalFileExists) throw new InvalidOperationException();
            if (previewStream.CanSeek) previewStream.Position = 0;
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;
            
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(previewKey)
                .WithStreamData(previewStream)
                .WithObjectSize(previewStream.CanSeek ? previewStream.Length : -1)
                .WithContentType(metadataDto.FormatName), ct);
            
            if (thumbnailStream.CanSeek) thumbnailStream.Position = 0;
            
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(thumbnailKey)
                .WithStreamData(thumbnailStream)
                .WithObjectSize(thumbnailStream.CanSeek ? thumbnailStream.Length : -1)
                .WithContentType("image/jpeg"), ct);
            
            var existingMetadata = await unitOfWork.MediaMetadata.FirstOrDefaultAsync(f => f.FileId == fileId, ct);

            if (existingMetadata != null)
            {
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
                var mediaMetadata = metadataDto.ToEntity(fileId, $"{bucketName}/{thumbnailKey}");
                mediaMetadata.ThumbnailPath = thumbnailKey;
                await unitOfWork.MediaMetadata.CreateAsync(mediaMetadata, ct);
            }

            var stat = await GetVersionInfo(objectName: previewKey, bucketName: bucketName, ct: ct);
            
            var existingPreview = await unitOfWork.Previews.FirstOrDefaultAsync(f => f.Path == filePath, ct);
            
            if (existingPreview != null)
            {
                existingPreview.Name =existingPreview.Name;
                existingPreview.Size = new BigInteger(stat.Size);
                existingPreview.UpdatedBy = "System";

                await unitOfWork.Previews.UpdateAsync(existingPreview, ct);
            }
            else
            {
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
        }
        catch (Exception e)
        {
            await unitOfWork.RollbackAsync(ct);
            // logger.LogError(e, "Failed to upload media data");
            try
            {
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(previewKey), ct);
                
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(thumbnailKey), ct);
            }
            catch (Exception cleanupEx)
            {
                // logger.LogWarning(cleanupEx, "Failed to clean up failed uploads");
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
                "application/vnd.oasis.opendocument.text" or "application/rtf" or "text/plain"
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
            "text/markdown" or "application/json" or "application/xml" or "text/xml" or "text/html"
                => FileCategory.Text,

            // ====== Fallback ======
            _ => FileCategory.Unknown
        };
    }

    public async Task<FileResultSummary?> GetCachedPreview(Guid id, CancellationToken ct = default)
    {
        var fileData = await unitOfWork.Files.GetFileWithPreviewAsync(id, ct);

        if (fileData is null) throw new FileNotFoundException();

        if (fileData.Preview is null) return null;

        var category = CategorizeFile(fileData.MimeType);

        switch (category)
        {
            case FileCategory.Image:
                return new FileResultSummary(
                    await DownloadFileNoCheckAsync(config.Value.PreviewBucket ?? "user-previews",
                        "previews/" + fileData.Name, ct), new FileSummary(fileData.Name, fileData.MimeType, true,
                        fileData.Preview.Path));
            case FileCategory.Document:
            case FileCategory.Spreadsheet:
            case FileCategory.Presentation:
            case FileCategory.Pdf:
                return new FileResultSummary(
                    await DownloadFileNoCheckAsync(config.Value.PreviewBucket ?? "user-previews",
                        "previews/" + fileData.Name + ".pdf", ct),
                    new FileSummary(fileData.Name, fileData.Preview.MimeType, true,
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
                        "previews/" + fileData.Name, ct), new FileSummary(fileData.Name, fileData.MimeType, true,
                        fileData.Preview.Path));
        }

        return null;
    }

    public async Task<FileResult> GetFileById(Guid id, CancellationToken ct)
    {
        var fileData = await GetFileMetadata(id, ct);
        if (fileData is null) throw new FileNotFoundException();

        //TODO: Remove magic number later, should be 20MB in bytes
        if (fileData.Size > 40971520) throw new InvalidOperationException("Filesize too large for preview");


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
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var path = $"{bucketName}/{objectName}";
            var fileEntity = await unitOfWork.Files.FirstOrDefaultAsync(f => f.Path == path, ct);

            if (fileEntity == null)
                throw new InvalidOperationException($"File with path {path} not found in database.");

            if (hardDelete)
            {
                // Remove from MinIO
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);

                // Hard delete from database
                unitOfWork.Files.Remove(fileEntity);
            }
            else
            {
                // Soft delete 
                unitOfWork.Files.Remove(fileEntity);
            }

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
        string? newName = null,
        bool? hasPreview = null,
        string? updatedBy = null,
        CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var fileEntity = await unitOfWork.Files.GetByIdAsync(fileId, ct);
            if (fileEntity == null) throw new InvalidOperationException($"File with ID {fileId} not found.");

            // Update only provided fields
            if (!string.IsNullOrEmpty(newName)) fileEntity.Name = newName;

            if (hasPreview.HasValue)
            {
                fileEntity.HasPreview = hasPreview.Value;
                if (hasPreview.Value) fileEntity.PreviewGeneratedAt = DateTime.UtcNow;
            }

            if (!string.IsNullOrEmpty(updatedBy)) fileEntity.UpdatedBy = updatedBy;

            var updatedFile = await unitOfWork.Files.UpdateAsync(fileEntity, ct);
            await unitOfWork.CommitAsync(ct);

            return updatedFile;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Update failed: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadFile(string? bucketName, string objectName, CancellationToken ct)
    {
        bucketName ??= config.Value.UploadBucket;
        // Verify file exists in database first
        var path = $"{bucketName}/{objectName}";
        var fileExists = await unitOfWork.Files.ExistsAsync(f => f.Path == path, ct);

        if (!fileExists) throw new InvalidOperationException($"File {path} not found in database.");

        // Download from MinIO
        var stream = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(stream)), ct);

        stream.Position = 0;
        return stream;
    }
    //TODO: This is probably introducing memory leaks as well and will need to be migrated to the direct stream pattern
    private async Task<Stream> DownloadFileNoCheckAsync(string? bucketName, string objectName, CancellationToken ct)
    {
        bucketName ??= config.Value.UploadBucket;
        // Verify file exists in database first
        var path = $"{bucketName}/{objectName}";
        // Download from MinIO
        var stream = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(stream)), ct);

        stream.Position = 0;
        return stream;
    }

    public async Task StreamFile(
        string fileId,
        Stream destination,
        CancellationToken ct)
    {
        var fileData = await unitOfWork.Files.GetByIdAsync(Guid.Parse(fileId), ct);

        if (fileData is null)
            throw new InvalidOperationException($"File with ID: {fileId} not found in database.");

        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(config.Value.UploadBucket)
            .WithObject(fileData.Name)
            .WithCallbackStream(s => s.CopyTo(destination)), ct);
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var found = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);
        if (!found) await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);

        // Enable versioning
        await minio.SetVersioningAsync(
            new SetVersioningArgs()
                .WithBucket(bucketName)
                .WithVersioningEnabled(), ct);
    }

    public Task DeleteFile(string bucketName, string objectName, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<VersionInfo> GetVersionInfo(string bucketName, string objectName, CancellationToken ct)
    {
        var stat = await minio.StatObjectAsync(
            new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName), ct);
        return new VersionInfo(stat.VersionId, stat.Size);
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