using DTO;
using DTO.Audit;
using DTO.Files;

namespace Common.Services;

public interface IAuditService
{
    Task Log(AuditLogDto action);

    Task<PaginatedResult<AuditLogResult>> GetLogs(AuditLogQuery query, Guid userId,
        CancellationToken ct = default);

    Task<ActivityStatisticsOverview> GetActivityOverview(Guid userId, ActivityQuery query, CancellationToken ct = default);
}