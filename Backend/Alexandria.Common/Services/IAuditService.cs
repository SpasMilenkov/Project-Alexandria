using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IAuditService
{
    Task Log(AuditLogDto action);

    Task<PaginatedResult<AuditLogResult>> GetLogs(AuditLogQuery query, Guid userId,
        CancellationToken ct = default);

    Task<ActivityStatisticsOverview> GetActivityOverview(Guid userId, ActivityQuery query,
        CancellationToken ct = default);
}