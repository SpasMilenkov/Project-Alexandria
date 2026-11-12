using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IAuditService
{
    /// <summary>The caller's own history: rows they did or that affected their data.</summary>
    Task<PaginatedResult<AuditLogResult>> GetLogsAsync(Guid userId, AuditLogQuery query,
        CancellationToken ct = default);

    Task<ActivityStatisticsOverview> GetActivityOverviewAsync(Guid userId, ActivityQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Search for the admin explorer. Other accounts' personal content never comes back (see
    /// AuditPersonalContent); every other read applies the same rule.
    /// </summary>
    Task<PaginatedResult<AuditLogResult>> SearchAsync(AuditViewer viewer, AuditLogFilter filter,
        AuditPageRequest page, CancellationToken ct = default);

    /// <summary>Rows about the entity itself plus rows whose parent it is (versions, previews, tags, links).</summary>
    Task<PaginatedResult<AuditLogResult>> GetEntityHistoryAsync(AuditViewer viewer,
        Data.Models.Enumerators.EntityType entityType, Guid entityId, AuditPageRequest page,
        CancellationToken ct = default);

    /// <summary>Every row written by one request or worker message, oldest first.</summary>
    Task<IReadOnlyList<AuditLogResult>> GetCorrelatedAsync(AuditViewer viewer, Guid correlationId,
        CancellationToken ct = default);

    /// <summary>Newest first, capped by configuration. Non-admin viewers are scoped to themselves.</summary>
    IAsyncEnumerable<AuditLogResult> ExportAsync(AuditViewer viewer, AuditLogFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// Splits what <see cref="ExportAsync"/> would return for the same filter into parts no larger
    /// than one export, each bounded by row positions. Uses the same viewer scope.
    /// </summary>
    Task<AuditExportPlan> PlanExportAsync(AuditViewer viewer, AuditLogFilter filter,
        CancellationToken ct = default);

    Task<SecuritySummary> GetSecuritySummaryAsync(Guid userId, CancellationToken ct = default);
}
