using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Repositories;

public interface IStreamHistoryRepository : IRepository<StreamHistory>
{
    Task<StreamHistory?> GetByUserAndFileAsync(Guid userId, Guid fileId, CancellationToken ct = default);
    Task<PaginatedResult<StreamHistory>> FindHistoryAsync(StreamHistoryQuery query, CancellationToken ct = default);

    Task UpsertPositionAsync(Guid userId, Guid fileId, long positionSeconds, bool completed,
        CancellationToken ct = default);

    Task<IEnumerable<StreamHistory>> GetRecentByUserAsync(Guid userId, int count, CancellationToken ct = default);
}