using File = Models.File;

namespace Infrastructure.Domain.Repositories;

public interface IFileRepository : IRepository<File>
{
    public Task<File> CreateAsync(File file, CancellationToken ct = default);
    public Task<File> UpdateAsync(File file, CancellationToken ct = default);
}