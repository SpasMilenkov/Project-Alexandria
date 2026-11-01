using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IOperationalEventRepository : IRepository<OperationalEvent>
{
    /// <summary>
    /// Retrieves a paginated, filtered list of operational events, ordered by creation time descending.
    /// </summary>
    /// <param name="query">
    /// Filter and paging options. Supports filtering by service, status, severity, code,
    /// creation-time range (<see cref="OperationalEventQuery.From"/> / <see cref="OperationalEventQuery.To"/>),
    /// and resolved/unresolved state (<see cref="OperationalEventQuery.Resolved"/>).
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A page of matching events plus paging metadata.</returns>
    Task<PaginatedResult<OperationalEvent>> GetEventsAsync(OperationalEventQuery query, CancellationToken ct = default);

    /// <summary>
    /// Lists the lifetimes of Failure-severity incidents that overlap <c>[from, to)</c>,
    /// optionally restricted to one service. Overlaps are left for the caller to merge.
    /// </summary>
    Task<List<DowntimeInterval>> GetDowntimeIntervalsAsync(ServiceType? service, DateTime from, DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the creation time, service and severity of every event created within <c>[from, to)</c>
    /// so the caller can bucket them by UTC day.
    /// </summary>
    Task<List<EventCalendarRow>> GetCalendarRowsAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the most recent active event for a given service and event code, if one exists.
    /// </summary>
    /// <param name="serviceType">The service to look up.</param>
    /// <param name="code">The event code to look up.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The most recent active event matching the service and code, or <c>null</c> if none is active.</returns>
    Task<OperationalEvent?> GetActiveEventAsync(
        ServiceType serviceType, OperationalEventCode code, CancellationToken ct);

    /// <summary>
    /// Resolves all currently active events matching the given service and code, setting their
    /// status to <see cref="OperationalEventStatus.Resolved"/> and stamping <c>ResolvedAt</c>/<c>UpdatedAt</c>.
    /// </summary>
    /// <param name="serviceType">The service whose active events should be resolved.</param>
    /// <param name="code">The event code whose active events should be resolved.</param>
    /// <param name="resolvedAt">Timestamp to record as the resolution time.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The number of events resolved.</returns>
    Task<int> ResolveActiveEventsAsync(
        ServiceType serviceType, OperationalEventCode code, DateTime resolvedAt, CancellationToken ct);

    /// <summary>
    /// Retrieves the current, highest-severity active event for each service that has one —
    /// a snapshot of overall system health.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>At most one active event per service.</returns>
    Task<List<OperationalEvent>> GetCurrentStatusPerServiceAsync(CancellationToken ct);

    /// <summary>
    /// Records that the active incident for a service and code recurred, so idle-based expiry
    /// measures time since the last occurrence rather than since the incident opened.
    /// </summary>
    /// <returns>The number of active events touched; zero when none is active.</returns>
    Task<int> TouchActiveEventAsync(
        ServiceType serviceType, OperationalEventCode code, DateTime seenAt, CancellationToken ct);

    /// <summary>
    /// Inserts and immediately saves an active event. Returns <c>false</c> instead of throwing when
    /// another producer already holds the active incident for the same service and code.
    /// Any other pending changes on the unit of work are saved with it.
    /// </summary>
    Task<bool> AddActiveIfAbsentAsync(OperationalEvent entity, CancellationToken ct);

    /// <summary>
    /// Retrieves tracked active events with the given code whose last occurrence is older than
    /// <paramref name="idleBefore"/>.
    /// </summary>
    Task<List<OperationalEvent>> GetIdleActiveEventsAsync(
        OperationalEventCode code, DateTime idleBefore, CancellationToken ct);
}
