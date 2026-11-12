using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage;

public class AuditService(IUnitOfWork unitOfWork, IOptions<AuditOptions> options) : IAuditService
{
    private const int CorrelatedRowLimit = 1000;
    private const int SecurityListSize = 10;
    private static readonly TimeSpan SecurityWindow = TimeSpan.FromDays(90);

    public async Task<PaginatedResult<AuditLogResult>> GetLogsAsync(Guid userId, AuditLogQuery query,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilter
        {
            ScopeUserId = userId,
            ReaderId = userId,
            EntityId = query.EntityId,

            // A file's history includes its versions, tags and links, matching the export and the
            // entity history view.
            IncludeChildren = query.EntityId.HasValue,
            EntityType = query.EntityType,
            OperationType = query.OperationType,
            Source = query.Source,
            IpAddress = query.IpAddress,
            From = query.After,
            To = query.Before,
        };

        var page = new AuditPageRequest
        {
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection,
        };

        return await unitOfWork.AuditLogs.FetchAuditLogAsync(filter, page, ct);
    }

    public async Task<ActivityStatisticsOverview> GetActivityOverviewAsync(Guid userId, ActivityQuery query,
        CancellationToken ct = default)
    {
        var rows = await unitOfWork.AuditLogs.GetActivityRowsAsync(userId, query, ct);
        return BuildOverview(rows);
    }

    public Task<PaginatedResult<AuditLogResult>> SearchAsync(AuditViewer viewer, AuditLogFilter filter,
        AuditPageRequest page, CancellationToken ct = default) =>
        unitOfWork.AuditLogs.FetchAuditLogAsync(Scope(viewer, filter), page, ct);

    public Task<PaginatedResult<AuditLogResult>> GetEntityHistoryAsync(AuditViewer viewer,
        EntityType entityType, Guid entityId, AuditPageRequest page, CancellationToken ct = default)
    {
        var filter = new AuditLogFilter
        {
            ScopeUserId = ScopeFor(viewer),
            ReaderId = viewer.UserId,
            EntityType = entityType,
            EntityId = entityId,
            IncludeChildren = true,
        };

        return unitOfWork.AuditLogs.FetchAuditLogAsync(filter, page, ct);
    }

    public async Task<IReadOnlyList<AuditLogResult>> GetCorrelatedAsync(AuditViewer viewer, Guid correlationId,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilter
        {
            ScopeUserId = ScopeFor(viewer),
            ReaderId = viewer.UserId,
            CorrelationId = correlationId,
        };

        return await unitOfWork.AuditLogs.GetRecentAsync(filter, CorrelatedRowLimit, SortDirection.Asc, ct);
    }

    private int ExportPartSize => Math.Max(1, options.Value.ExportMaxRows);

    public IAsyncEnumerable<AuditLogResult> ExportAsync(AuditViewer viewer, AuditLogFilter filter,
        CancellationToken ct = default)
    {
        return unitOfWork.AuditLogs.StreamAsync(Scope(viewer, filter), ExportPartSize, ct);
    }

    // Walks the matching rows newest first, one part at a time: each step skips a part's worth of
    // positions to find where the next part starts, so the plan costs one pass over the positions
    // and never loads the rows themselves.
    public Task<AuditExportPlan> PlanExportAsync(AuditViewer viewer, AuditLogFilter filter,
        CancellationToken ct = default)
    {
        return PlanPartsAsync(unitOfWork.AuditLogs, Scope(viewer, filter), ExportPartSize, ct);
    }

    internal static async Task<AuditExportPlan> PlanPartsAsync(IAuditLogRepository logs, AuditLogFilter scoped,
        int partSize, CancellationToken ct)
    {
        var parts = new List<AuditExportPart>();

        var newest = await logs.GetPositionsAsync(scoped, 0, 1, ct: ct);
        if (newest.Count == 0)
            return new AuditExportPlan { Total = 0, PartSize = partSize, Parts = parts };

        var start = newest[0];

        while (true)
        {
            var remaining = scoped with { UpTo = start };

            // The part's last row and, when there is one, the next part's first row.
            var edge = await logs.GetPositionsAsync(remaining, partSize - 1, 2, ct: ct);

            if (edge.Count == 2)
            {
                parts.Add(new AuditExportPart
                {
                    Index = parts.Count + 1,
                    Rows = partSize,
                    StartAt = start.Timestamp,
                    StartId = start.Id,
                    EndAt = edge[1].Timestamp,
                    EndId = edge[1].Id,
                    OldestAt = edge[0].Timestamp,
                });

                start = edge[1];
                continue;
            }

            var rows = await logs.CountAsync(remaining, ct);
            var oldest = await logs.GetPositionsAsync(remaining, 0, 1, SortDirection.Asc, ct);

            parts.Add(new AuditExportPart
            {
                Index = parts.Count + 1,
                Rows = rows,
                StartAt = start.Timestamp,
                StartId = start.Id,
                OldestAt = oldest.Count > 0 ? oldest[0].Timestamp : start.Timestamp,
            });

            break;
        }

        return new AuditExportPlan { Total = parts.Sum(p => p.Rows), PartSize = partSize, Parts = parts };
    }

    // Each figure gets its own bounded query, so a burst of failed attempts cannot crowd real
    // sign-ins out of the summary or cap the failure count.
    public async Task<SecuritySummary> GetSecuritySummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now - SecurityWindow;

        var logins = await unitOfWork.AuditLogs.GetRecentAsync(new AuditLogFilter
        {
            OwnerId = userId,
            EventCodes = [AuditEventCode.UserLogin],
            From = windowStart,
        }, SecurityListSize, ct: ct);

        var failures = await unitOfWork.AuditLogs.GetRecentAsync(new AuditLogFilter
        {
            OwnerId = userId,
            EventCodes = [AuditEventCode.UserLoginFailed],
            From = windowStart,
        }, SecurityListSize, ct: ct);

        var failedLastMonth = await unitOfWork.AuditLogs.CountAsync(new AuditLogFilter
        {
            OwnerId = userId,
            EventCodes = [AuditEventCode.UserLoginFailed],
            From = now.AddDays(-30),
        }, ct);

        var addresses = await unitOfWork.AuditLogs.GetSignInHistoryAsync(userId, windowStart, ct);

        return new SecuritySummary
        {
            RecentLogins = logins,
            RecentFailedAttempts = failures,
            FailedAttemptsLast30Days = failedLastMonth,
            KnownIpAddresses = ToKnownIpAddresses(addresses),
        };
    }

    internal static Guid? ScopeFor(AuditViewer viewer) => viewer.IsAdmin ? null : viewer.UserId;

    // Non-admins can narrow their own rows but never widen past them, whatever the filter says.
    // Admins search every account, but only its account and security rows: no library shows up,
    // not even the admin's own, which their personal activity page already covers.
    internal static AuditLogFilter Scope(AuditViewer viewer, AuditLogFilter filter) => new()
    {
        ScopeUserId = ScopeFor(viewer),
        ReaderId = viewer.UserId,
        ExcludePersonalContent = viewer.IsAdmin,
        UserId = viewer.IsAdmin ? filter.UserId : null,
        OwnerId = viewer.IsAdmin ? filter.OwnerId : null,
        EntityType = filter.EntityType,
        EntityId = filter.EntityId,
        IncludeChildren = filter.IncludeChildren,
        EventCodes = filter.EventCodes,
        OperationType = filter.OperationType,
        Source = filter.Source,
        IpAddress = filter.IpAddress,
        From = filter.From,
        To = filter.To,
        CorrelationId = filter.CorrelationId,
        UpTo = filter.UpTo,
        After = filter.After,
    };

    internal static ActivityStatisticsOverview BuildOverview(IReadOnlyList<AuditActivityRow> rows)
    {
        var perDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp.UtcDateTime))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => new ActivitySummary
            {
                TotalOperations = g.Count(),
                CountPerOperation = g.GroupBy(r => r.OperationType).ToDictionary(o => o.Key, o => o.Count())
            });

        return new ActivityStatisticsOverview
        {
            TotalActivity = rows.Count,
            ActivityPerDay = perDay
        };
    }

    internal static List<KnownIpAddress> ToKnownIpAddresses(IReadOnlyList<AuditSignInHistoryRow> addresses) =>
        addresses
            .Where(h => !string.IsNullOrEmpty(h.IpAddress))
            .Select(h => new KnownIpAddress
            {
                IpAddress = h.IpAddress!,
                LoginCount = h.SignIns,
                FirstSeen = h.FirstSeen,
                LastSeen = h.LastSeen,
            })
            .OrderByDescending(ip => ip.LastSeen)
            .ToList();
}
