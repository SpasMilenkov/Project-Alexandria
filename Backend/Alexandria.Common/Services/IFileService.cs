using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using File = Alexandria.Data.Models.File;


namespace Alexandria.Common.Services;

/// <summary>Per-item outcome of a bulk metadata update (partial success expected).</summary>
public sealed record FileMetadataUpdateResult(Guid FileId, bool Success, string? Error);

public interface IFileService
{
    Task FolderWithOwnershipExistsAsync(Guid? directoryId, Guid ownerId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);

    Task<File?> GetFileMetadataAsync(Guid fileId, CancellationToken ct = default);
    Task<string?> VersionBelongsToUserAsync(Guid versionId, Guid userId, CancellationToken ct = default);

    Task<FileResult> GetFileWithOwnershipByIdAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task DeleteFilesAsync(Guid[] fileIds, Guid userId, bool hardDelete = false, CancellationToken ct = default);

    Task<File> UpdateFileMetadataAsync(
        Guid fileId,
        Guid updatedBy,
        string? newName = null,
        string? newTitle = null,
        string? newArtist = null,
        string? newAlbum = null,
        string? newYear = null,
        CancellationToken ct = default);

    /// <summary>
    /// Applies the same partial metadata update to each file independently (per-item commit).
    /// Ownership is enforced per item; failures are reported per item rather than aborting
    /// the batch. Bulk edits are owner-scoped only (no admin override, unlike single edit).
    /// </summary>
    Task<IReadOnlyList<FileMetadataUpdateResult>> BulkUpdateFileMetadataAsync(
        Guid[] fileIds,
        Guid updatedBy,
        string? newTitle = null,
        string? newArtist = null,
        string? newAlbum = null,
        string? newYear = null,
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

    Task<Guid?> GetCurrentVersionIdAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task ChangeActiveVersionAsync(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task RemoveFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default);
    Task RestoreFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default);

    Task<bool> IsVideo(
        Guid versionId,
        Guid userId,
        CancellationToken ct = default);

    Task<PaginatedResult<MediaFileDto>> GetFilesForStreamingAsync(Guid userId, int page, int pageSize,
        string? query = null, Guid? playlistId = null, bool isVideo = false,
        CancellationToken ct = default);
}