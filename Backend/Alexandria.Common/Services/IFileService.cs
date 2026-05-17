using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using File = Alexandria.Data.Models.File;


namespace Alexandria.Common.Services;

public interface IFileService
{
    Task FolderWithOwnershipExistsAsync(Guid? directoryId, Guid ownerId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task<File?> GetFileMetadataAsync(Guid fileId, CancellationToken ct = default);
    Task<bool> VersionBelongsToUserAsync(Guid versionId, Guid userId, CancellationToken ct = default);

    Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task<FileResult> GetFileWithOwnershipByIdAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task DeleteFilesAsync(Guid[] fileIds, Guid userId, bool hardDelete = false, CancellationToken ct = default);

    Task<File> UpdateFileMetadataAsync(
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

    Task<PaginatedResult<FileResult>> GetFilesByDirectoryIdAsync(
        Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> SearchFileAsync(FileSearchQuery query, Guid userId,
        CancellationToken ct = default);

    Task<int> GetFileCountAsync(string? mimeTypeFilter = null, CancellationToken ct = default);
    Task<int> RestoreFilesAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<int> GetFileCountPerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default);
    Task<long> GetFileSizePerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default);

    Task<PaginatedResult<FileVersionDto>> GetVersionsForFileAsync(Guid fileId, Guid userId, int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task ChangeActiveVersionAsync(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task RemoveFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default);
    Task RestoreFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default);

    Task<bool> IsVideo(
        Guid versionId,
        Guid userId,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetFilesForStreamingAsync(Guid userId, int page, int pageSize,
        CancellationToken ct = default);
}