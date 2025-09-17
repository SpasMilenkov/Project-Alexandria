using Infrastructure.Domain.DomainObjects;
using File = Models.File;

namespace Infrastructure.Domain.Services;

public interface IStorageService
{
    // File Upload
    Task<UploadResult> UploadFile(
        string bucketName,
        string objectName,
        string contentType,
        Stream fileStream,
        CancellationToken ct,
        long contentLength = -1,
        string? originalFileName = null,
        string? uploadedBy = null);

    // File Download
    Task<Stream> DownloadFile(string bucketName, string objectName, CancellationToken ct);

    // File Metadata Operations
    Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default);
    Task<File?> GetFileByPath(string path, CancellationToken ct = default);
    Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default);
    Task<IEnumerable<File>> GetAllFiles(CancellationToken ct = default);
    Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default);

    // File Operations
    Task DeleteFile(string bucketName, string objectName, CancellationToken ct, bool hardDelete = false);

    Task<File> UpdateFileMetadata(
        Guid fileId,
        string? newName = null,
        bool? hasPreview = null,
        string? updatedBy = null,
        CancellationToken ct = default);

    // Storage Management
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct);
    Task<VersionInfo> GetVersionInfo(string bucketName, string objectName, CancellationToken ct);
}