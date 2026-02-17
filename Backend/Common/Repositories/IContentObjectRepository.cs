using Models;

namespace Common.Repositories;

public interface IContentObjectRepository : IRepository<ContentObject>
{
    Task<ContentObject?> HashExists(byte[] hash, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> MarkOrphaned(DateTime time, CancellationToken ct);
    Task<int> ClearOrphaned(DateTime time, CancellationToken ct);
}