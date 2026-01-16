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
    Task<File?> GetFileWithTagsAsync(Guid fileId, CancellationToken ct = default);

    Task<PaginatedResult<FileResult>> GetFilesByDirectoryIdAsync(
        Guid parentDirectoryId,
        Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default);

    Task<(IEnumerable<File> Files, int TotalCount)> FindFilesByTagsAsync(
        FileTagSearchQuery query,
        CancellationToken ct = default);

    Task<File?> GetFileWithPreviewAsync(Guid fileId, CancellationToken ct = default);
    Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default);
    Task HasDuplicatesAsync(Guid[] fileIds, Guid destinationId, Guid userId, CancellationToken ct = default);
    Task MarkAsDeleted(Guid[] fileIds, Guid userId, CancellationToken ct = default);

    Task CopyFilesAsync(
        Guid[] fileIds,
        Guid destinationId,
        Guid userId,
        CancellationToken ct);
}