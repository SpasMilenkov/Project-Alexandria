using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IAuditService
{
    Task LogAsync(AuditLogDto action);

    Task<PaginatedResult<AuditLogResult>> GetLogsAsync(AuditLogQuery query, Guid userId,
        CancellationToken ct = default);

    Task<ActivityStatisticsOverview> GetActivityOverviewAsync(Guid userId, ActivityQuery query,
        CancellationToken ct = default);
}