using DTO;
using DTO.Files;
using DTO.Tags;
using File = Models.File;

namespace Common.Repositories;

public interface IFileRepository : IRepository<File>
{
    public Task<File> CreateAsync(File file, CancellationToken ct = default);
    public Task<File> UpdateAsync(File file, CancellationToken ct = default);
    public Task<File?> GetFileWithTagsAsync(Guid fileId, CancellationToken ct = default);
    public Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default);

    Task<(IEnumerable<File> Files, int TotalCount)> FindFilesByTagsAsync(
        FileTagSearchQuery query, 
        CancellationToken ct = default);
    
    public Task<File> GetFileWithPreviewAsync(Guid fileId, CancellationToken ct = default);
}