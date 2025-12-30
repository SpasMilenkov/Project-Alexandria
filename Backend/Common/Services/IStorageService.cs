using DTO;
using DTO.Files;
using Models.Enumerators;
using File = Models.File;
using MediaMetadata = DTO.Files.MediaMetadata;

namespace Common.Services;

public interface IStorageService
{
    // File Upload
    Task<UploadResult> UploadFile(string bucketName,
        string objectName,
        string contentType,
        string clientSha256,
        Stream fileStream,
        Guid uploadedBy,
        CancellationToken ct = default,
        long contentLength = -1L,
        Guid? directoryId = null,
        string? originalFileName = null);

    public Task<UploadResult> UploadPreview(
        string bucketName,
        string objectName,
        string contentType,
        Stream fileStream,
        Guid originalFileId,
        Guid uploadedBy,
        long contentLength = -1,
        string? originalFileName = null,
        CancellationToken ct = default);

    // Task UploadThumbnailAsync(
    //     string objectName,
    //     Stream fileStream,
    //     long contentLength = -1,
    //     CancellationToken ct = default);

    Task UploadMediaData(Stream previewStream, Stream thumbnailStream, string objectName, Guid fileId,
        MediaMetadata metadataDto,
        CancellationToken ct = default);
    // File Download
    Task<Stream> DownloadFile(Guid fileId, Guid ownerId, CancellationToken ct);
    Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct);
    Task StreamFile(
        string fileId,
        Stream destination,
        CancellationToken ct);
    Task<PreviewResultDto?> GetCachedPreview(Guid id, CancellationToken ct);

    public string GetPreviewPresignedUrl(string objectKey, TimeSpan expiry);
    //TODO: Determine if this is worth keeping
    // Task<FileResult> GetFileById(Guid id, CancellationToken ct);

    FileCategory CategorizeFile(string mimeType);
    // File Metadata Operations
    Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default);
    Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default);
    Task<IEnumerable<File>> GetAllFiles(CancellationToken ct = default);
    Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default);

    // File Operations
    Task DeleteFile(Guid fileId, Guid userId, CancellationToken ct, bool hardDelete = false);

    Task<File> UpdateFileMetadata(
        Guid fileId,
        Guid updatedBy,
        string? newName = null,
        bool? hasPreview = null,
        CancellationToken ct = default);

    // Storage Management
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct);
    Task<VersionInfo> GetVersionInfo(string bucketName, string objectName, CancellationToken ct);
    
    // File Data Management
    Task<FileSummary?> GetFileSummary(Guid fieldId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetRootFilesAsync(Guid ownerId, int page = 1, int pageSize = 25, SortBy sortBy = SortBy.Name, SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetFilesByDirectoryId(Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);
}