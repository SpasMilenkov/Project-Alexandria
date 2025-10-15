using DTO;
using File = Models.File;

namespace Common;

public interface IFileRepository : IRepository<File>
{
    public Task<File> CreateAsync(File file, CancellationToken ct = default);
    public Task<File> UpdateAsync(File file, CancellationToken ct = default);

    public Task<FileSummary?> GetFileNameAndMimeType(Guid fileId, CancellationToken ct = default);

    public Task<File> GetFileWithPreviewAsync(Guid fileId, CancellationToken ct = default);
}