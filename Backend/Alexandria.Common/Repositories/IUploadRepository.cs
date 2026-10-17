using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IUploadRepository : IRepository<Upload>
{
    /// <summary>
    /// Batch lookup for the temp sweeper. Returns live rows for the given temp
    /// object keys. Tracked, so the sweeper can flip abandoned rows to
    /// <c>Interrupted</c> in the same scope.
    /// </summary>
    Task<List<Upload>> GetByTempKeysAsync(IEnumerable<string> tempKeys, CancellationToken ct = default);
}