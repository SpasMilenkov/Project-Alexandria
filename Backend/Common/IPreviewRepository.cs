using Models;

namespace Common;

public interface IPreviewRepository : IRepository<Preview>
{
    public Task<Preview> CreateAsync(Preview file, CancellationToken ct = default);
    public Task<Preview> UpdateAsync(Preview file, CancellationToken ct = default);
}