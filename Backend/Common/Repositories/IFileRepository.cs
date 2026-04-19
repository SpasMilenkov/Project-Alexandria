using DTO.Files;
using DTO.Tags;
using Models.Enumerators;
using File = Models.File;

namespace Common.Repositories;

public interface IFileRepository : IRepository<File>
{
    Task<File> CreateAsync(File file, CancellationToken ct = default);
    Task<byte[]?> GetFileHash(Guid fileId, Guid ownerId, CancellationToken ct = default);
    Task<string> GetFileHashAsString(Guid fileId, Guid ownerId, CancellationToken ct = default);
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

    Task<(DownloadMetadata fileMetadata, byte[] fileHash)?> GetDownloadMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default);
    Task<FileMetadata?> GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query,
        CancellationToken ct = default);

    Task<File?> GetFileWithPreviewAsync(Guid fileId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task HasDuplicatesAsync(Guid[] fileIds, Guid destinationId, Guid userId, CancellationToken ct = default);
    Task MarkAsDeleted(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<bool> IsPromoted(Guid fileId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> FindFiles(FileSearchQuery query, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(
        Guid[] fileIds,
        Guid? destinationId,
        Guid userId,
        CancellationToken ct);

    Task<long> GetDeletedSize(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<FileSummary>> GetOldFiles(Guid userId, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetSizeByType(Guid userId, CancellationToken ct = default);
    Task<int> RestoreFiles(Guid[] fileIds, Guid userId, CancellationToken ct = default);
    Task<int> GetFileCountPerUser(Guid userId, bool deletedOnly, CancellationToken ct = default);
    Task<long> GetStorageUsagePerUser(Guid userId, bool onlyDeleted, CancellationToken ct = default);
    Task ChangeActiveVersion(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default);
    Task<FileResult?> GetFileWithOwnershipById(Guid fileId, Guid userId, CancellationToken ct = default);
    Task UpdateCurrentVersion(Guid fileId, Guid versionId, CancellationToken ct = default);
}
