using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IContentObjectRepository : IRepository<ContentObject>
{
    Task<ContentObject?> HashExistsAsync(byte[] hash, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> MarkOrphaned(DateTime time, CancellationToken ct = default);
    Task<int> ClearOrphaned(DateTime time, CancellationToken ct = default);
}