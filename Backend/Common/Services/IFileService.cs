using DTO.Files;
using Models.Enumerators;
using File = Models.File;


namespace Common.Services;

public interface IFileService
{
    Task FolderWithOwnershipExists(Guid? directoryId, Guid ownerId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(Guid[] fileIds, Guid destinationId, Guid userId, CancellationToken ct = default);

    Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default);

    Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    Task<FileSummary?> GetFileSummary(Guid fieldId, CancellationToken ct = default);

    Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default);

    Task DeleteFiles(Guid[] fileIds, Guid userId, bool hardDelete = false, CancellationToken ct = default);

    Task<File> UpdateFileMetadata(
        Guid fileId,
        Guid updatedBy,
        string? newName = null,
        bool? hasPreview = null,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetRootFilesAsync(
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetFilesByDirectoryId(
        Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default);
}