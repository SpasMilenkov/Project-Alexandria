using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogDto dto, CancellationToken ct = default);
    Task<PaginatedResult<AuditLogResult>> FetchAuditLogAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>
    /// Used for the deletion of the old Audit logs
    /// When run will delete all logs older than 2 years ago
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task DeleteRangeAsync(CancellationToken ct = default);

    Task<ActivityStatisticsOverview> GetOverviewAsync(Guid userId, ActivityQuery query, CancellationToken ct = default);
}