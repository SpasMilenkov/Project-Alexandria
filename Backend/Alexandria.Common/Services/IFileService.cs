using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using File = Alexandria.Data.Models.File;


namespace Alexandria.Common.Services;

public interface IFileService
{
    Task FolderWithOwnershipExists(Guid? directoryId, Guid ownerId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default);

    Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task<FileResult> GetFileWithOwnershipById(Guid fileId, Guid userId, CancellationToken ct = default);
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

    Task<PaginatedResult<FileResult>> SearchFile(FileSearchQuery query, Guid userId, CancellationToken ct = default);

    Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default);
    Task<int> RestoreFiles(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<int> GetFileCountPerUser(Guid userId, bool deletedOnly, CancellationToken ct = default);
    Task<long> GetFileSizePerUser(Guid userId, bool deletedOnly, CancellationToken ct = default);

    Task<PaginatedResult<FileVersionDto>> GetVersionsForFile(Guid fileId, Guid userId, int page = 1, int pageSize = 10,
        CancellationToken ct = default);

    Task ChangeActiveVersion(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task RemoveFileVersion(Guid fileVersionId, Guid userId, CancellationToken ct = default);
    Task RestoreFileVersion(Guid fileVersionId, Guid userId, CancellationToken ct = default);
}