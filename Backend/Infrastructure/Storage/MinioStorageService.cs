using System.Numerics;
using Common;
using Infrastructure.Domain.DomainObjects;
using Infrastructure.Domain.Repositories;
using Infrastructure.Domain.Services;
using Minio;
using Minio.DataModel.Args;
using File = Models.File;

namespace Infrastructure.Storage;

public class MinioStorageService(
    IMinioClient minio,
    IFileRepository fileRepository,
    IUnitOfWork unitOfWork)
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

            var existingFile = await fileRepository.FirstOrDefaultAsync(f => f.Path == filePath, ct);

            File savedFile;

            if (existingFile != null)
            {
                existingFile.Name = originalFileName ?? existingFile.Name;
                existingFile.Size = new BigInteger(stat.Size);
                existingFile.UpdatedBy = uploadedBy;

                savedFile = await fileRepository.UpdateAsync(existingFile, ct);
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

                savedFile = await fileRepository.CreateAsync(fileEntity, ct);
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

    public async Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default)
    {
        return await fileRepository.GetByIdAsync(fileId, ct);
    }

    public async Task<File?> GetFileByPath(string path, CancellationToken ct = default)
    {
        return await fileRepository.FirstOrDefaultAsync(f => f.Path == path, ct);
    }

    public async Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default)
    {
        return await fileRepository.FindAsync(f => f.MimeType == mimeType, ct);
    }

    public async Task DeleteFile(string bucketName, string objectName, CancellationToken ct, bool hardDelete = false)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var path = $"{bucketName}/{objectName}";
            var fileEntity = await fileRepository.FirstOrDefaultAsync(f => f.Path == path, ct);

            if (fileEntity == null)
            {
                throw new InvalidOperationException($"File with path {path} not found in database.");
            }

            if (hardDelete)
            {
                // Remove from MinIO
                await minio.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName), ct);

                // Hard delete from database
                fileRepository.Remove(fileEntity);
            }
            else
            {
                // Soft delete 
                fileRepository.Remove(fileEntity);
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
            var fileEntity = await fileRepository.GetByIdAsync(fileId, ct);
            if (fileEntity == null)
            {
                throw new InvalidOperationException($"File with ID {fileId} not found.");
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(newName))
            {
                fileEntity.Name = newName;
            }

            if (hasPreview.HasValue)
            {
                fileEntity.HasPreview = hasPreview.Value;
                if (hasPreview.Value)
                {
                    fileEntity.PreviewGeneratedAt = DateTime.UtcNow;
                }
            }

            if (!string.IsNullOrEmpty(updatedBy))
            {
                fileEntity.UpdatedBy = updatedBy;
            }

            var updatedFile = await fileRepository.UpdateAsync(fileEntity, ct);
            await unitOfWork.CommitAsync(ct);

            return updatedFile;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Update failed: {ex.Message}", ex);
        }
    }

    public async Task<Stream> DownloadFile(string bucketName, string objectName, CancellationToken ct)
    {
        // Verify file exists in database first
        var path = $"{bucketName}/{objectName}";
        var fileExists = await fileRepository.ExistsAsync(f => f.Path == path, ct);

        if (!fileExists)
        {
            throw new InvalidOperationException($"File {path} not found in database.");
        }

        // Download from MinIO
        var stream = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(stream)), ct);

        stream.Position = 0;
        return stream;
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var found = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);
        if (!found)
        {
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
        }

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
        return await fileRepository.GetAllAsync(ct);
    }

    public async Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(mimeTypeFilter))
        {
            return await fileRepository.CountAsync(null, ct);
        }

        return await fileRepository.CountAsync(f => f.MimeType == mimeTypeFilter, ct);
    }
}