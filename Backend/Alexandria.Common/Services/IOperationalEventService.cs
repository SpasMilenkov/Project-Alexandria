using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Files;
using Alexandria.Dto.Status;

namespace Alexandria.Common.Services;

public interface IOperationalEventService
{
    Task<PaginatedResult<OperationalEvent>> GetEventsAsync(OperationalEventQuery query, CancellationToken ct);

    Task<List<ErrorAggregate>> GetErrorCountsAsync(DateTime from, DateTime to, CancellationToken ct);

    Task<double> GetUptimeAsync(ServiceType service, DateTime from, DateTime to, CancellationToken ct);

    Task<List<ServiceUptime>> GetAllUptimesAsync(DateTime from, DateTime to, CancellationToken ct);

    Task<List<OperationalEvent>> GetCurrentStatusAsync(CancellationToken ct);

    Task<OperationalEvent> ReportProblemAsync(Guid userId, string description, string? pageContext,
        CancellationToken ct);

    /// <summary>
    /// Marks an active incident as manually resolved. Appends the optional
    /// resolution note into the event's metadata blob. Returns NotFound when the
    /// id does not exist and AlreadyResolved when the incident is not active.
    /// </summary>
    Task<ResolveIncidentResult> ResolveAsync(Guid eventId, Guid resolvedBy, string? note,
        CancellationToken ct);

    /// <summary>
    /// Builds the anonymous-facing daily status history: for each service, a
    /// chronological list of daily three-state statuses over the last
    /// <paramref name="days"/> days (ending today, UTC). Events overlapping a day
    /// mark it Down (any Failure) or Degraded (any other severity).
    /// </summary>
    Task<IReadOnlyList<DailyServiceStatus>> GetPublicStatusHistoryAsync(int days,
        CancellationToken ct);

    /// <summary>
    /// Resolves active incidents of <paramref name="code"/> whose last occurrence is older than
    /// <paramref name="idleBefore"/>, appending an auto-expiry resolution note.
    /// </summary>
    /// <returns>The number of incidents resolved.</returns>
    Task<int> ExpireIdleIncidentsAsync(OperationalEventCode code, DateTime idleBefore, CancellationToken ct);
}
