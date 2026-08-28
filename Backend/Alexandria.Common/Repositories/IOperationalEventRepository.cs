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
    /// Calculates the uptime percentage for a service over a time window, based on the overlap
    /// of healthcheck-unhealthy/unreachable events with that window.
    /// </summary>
    /// <param name="service">The service to calculate uptime for.</param>
    /// <param name="from">Start of the time window.</param>
    /// <param name="to">End of the time window.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>Uptime percentage (0-100) for the service over the given window.</returns>
    Task<double> CalculateUptimeAsync(ServiceType service, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Aggregates the number of events created per day, per service, within a time range.
    /// </summary>
    /// <param name="from">Inclusive start of the range.</param>
    /// <param name="to">Inclusive end of the range.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>Daily, per-service event counts.</returns>
    Task<List<ErrorAggregate>> GetErrorCountsAsync(DateTime from, DateTime to, CancellationToken ct = default);

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
}