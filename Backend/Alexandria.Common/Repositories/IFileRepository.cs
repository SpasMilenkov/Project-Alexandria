using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Common.Repositories;

public interface IFileRepository : IRepository<File>
{
    Task<File> CreateAsync(File file, CancellationToken ct = default);
    Task<byte[]?> GetFileHashAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<string> GetFileHashAsStringAsync(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<File> UpdateAsync(File file, CancellationToken ct = default);
    Task<FileResult?> GetFileWithTagsAsync(Guid userId, Guid fileId, CancellationToken ct = default);

    Task<File?> GetFileEntityWithTagsAsync(
        Guid fileId,
        CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetFilesByDirectoryIdAsync(
        Guid parentDirectoryId,
        Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    Task<(DownloadMetadata fileMetadata, byte[] fileHash)?> GetDownloadMetadataAsync(Guid fileId, Guid userId,
        CancellationToken ct = default);

    Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query,
        CancellationToken ct = default);

    Task<File?> GetFileWithPreviewAsync(Guid fileId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task MarkAsDeletedAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<bool> IsPromotedAsync(Guid fileId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>>
        FindFilesAsync(FileSearchQuery query, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(
        Guid[] fileIds,
        Guid? destinationId,
        Guid userId,
        CancellationToken ct);

    Task<long> GetDeletedSizeAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<FileSummary>> GetOldFilesAsync(Guid userId, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetSizeByTypeAsync(Guid userId, CancellationToken ct = default);
    Task<int> RestoreFilesAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<int> GetFileCountPerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default);
    Task<long> GetStorageUsagePerUserAsync(Guid userId, bool onlyDeleted, CancellationToken ct = default);
    Task ChangeActiveVersionAsync(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task<FileResult?> GetFileWithOwnershipByIdAsync(Guid fileId, Guid userId, CancellationToken ct = default);
    Task UpdateCurrentVersionAsync(Guid fileId, Guid versionId, CancellationToken ct = default);
}