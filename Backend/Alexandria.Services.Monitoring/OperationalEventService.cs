using System.Text.Json;
using System.Text.Json.Nodes;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Status;

namespace Alexandria.Services.Monitoring;

public class OperationalEventService(
    IUnitOfWork unitOfWork) : IOperationalEventService
{
    internal const string AutoExpiredNote = "Auto-expired: no recurrence within the idle window.";

    public Task<PaginatedResult<OperationalEvent>> GetEventsAsync(OperationalEventQuery query, CancellationToken ct)
        => unitOfWork.OperationalEvents.GetEventsAsync(query, ct);

    public async Task<List<ErrorAggregate>> GetErrorCountsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await unitOfWork.OperationalEvents.GetCalendarRowsAsync(from, to, ct);

        // Bucketed in C# so the day boundary is always UTC, independent of the database session time zone.
        return rows
            .GroupBy(r => new { Day = DateOnly.FromDateTime(r.CreatedAt), r.ServiceType, r.Severity })
            .Select(g => new ErrorAggregate(g.Key.Day, g.Key.ServiceType, g.Key.Severity, g.Count()))
            .ToList();
    }

    public async Task<double> GetUptimeAsync(ServiceType service, DateTime from, DateTime to, CancellationToken ct)
    {
        var intervals = await unitOfWork.OperationalEvents.GetDowntimeIntervalsAsync(service, from, to, ct);
        return UptimeCalculator.Calculate(intervals, from, to, DateTime.UtcNow);
    }

    public async Task<List<ServiceUptime>> GetAllUptimesAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var intervals = await unitOfWork.OperationalEvents.GetDowntimeIntervalsAsync(null, from, to, ct);
        var now = DateTime.UtcNow;

        return Enum.GetValues<ServiceType>()
            .Select(service => new ServiceUptime(
                service,
                UptimeCalculator.Calculate(intervals.Where(i => i.ServiceType == service), from, to, now)))
            .ToList();
    }

    public Task<List<OperationalEvent>> GetCurrentStatusAsync(CancellationToken ct)
        => unitOfWork.OperationalEvents.GetCurrentStatusPerServiceAsync(ct);

    public async Task<OperationalEvent> ReportProblemAsync(Guid userId, string description, string? pageContext,
        CancellationToken ct)
    {
        var evt = new OperationalEvent
        {
            Id = Guid.NewGuid(),
            ServiceType = ServiceType.Api,
            Code = OperationalEventCode.UserReportedProblem,
            Severity = OperationalEventSeverity.PartialFailure,
            Status = OperationalEventStatus.Active,
            MetadataJson = OperationalEventExtensions.SerializeMetadata(
                OperationalEventCode.UserReportedProblem,
                new UserReportedMetadata
                {
                    ServiceInstance = "user-report",
                    Description = description,
                    PageContext = pageContext
                }),
            CreatedBy = userId
        };

        await unitOfWork.OperationalEvents.AddAsync(evt, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return evt;
    }

    public async Task<ResolveIncidentResult> ResolveAsync(Guid eventId, Guid resolvedBy, string? note,
        CancellationToken ct)
    {
        var evt = await unitOfWork.OperationalEvents.GetByIdAsync(eventId, ct);
        if (evt is null)
            return new ResolveIncidentResult(ResolveOutcome.NotFound, null);

        if (evt.Status != OperationalEventStatus.Active)
            return new ResolveIncidentResult(ResolveOutcome.AlreadyResolved, evt);

        if (!string.IsNullOrWhiteSpace(note))
            evt.MetadataJson = AppendResolutionNote(evt.MetadataJson, note.Trim());

        var now = DateTime.UtcNow;
        evt.Status = OperationalEventStatus.Resolved;
        evt.ResolvedAt = now;
        evt.UpdatedAt = now;
        evt.UpdatedBy = resolvedBy;

        await unitOfWork.SaveChangesAsync(ct);
        return new ResolveIncidentResult(ResolveOutcome.Resolved, evt);
    }

    public async Task<int> ExpireIdleIncidentsAsync(OperationalEventCode code, DateTime idleBefore,
        CancellationToken ct)
    {
        var idle = await unitOfWork.OperationalEvents.GetIdleActiveEventsAsync(code, idleBefore, ct);
        if (idle.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        foreach (var evt in idle)
        {
            evt.MetadataJson = AppendResolutionNote(evt.MetadataJson, AutoExpiredNote);
            evt.Status = OperationalEventStatus.Resolved;
            evt.ResolvedAt = now;
            evt.UpdatedAt = now;
            evt.UpdatedBy = SystemConfig.SystemId;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return idle.Count;
    }

    // Format-preserving append: parses whatever shape the blob has (modern
    // camelCase+kind or anything else) and adds one key without touching the rest.
    private static string AppendResolutionNote(string? metadataJson, string note)
    {
        JsonObject node;
        try
        {
            node = JsonNode.Parse(metadataJson ?? "{}") as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            node = new JsonObject();
        }

        node["resolutionNote"] = note;
        return node.ToJsonString();
    }

    public async Task<IReadOnlyList<DailyServiceStatus>> GetPublicStatusHistoryAsync(int days,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddDays(-(days - 1));
        var windowStartUtc = DateTime.SpecifyKind(windowStart.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        // An event overlaps the window when it started before "now" and was
        // still active at the window's first instant.
        var events = (await unitOfWork.OperationalEvents.FindAsync(
            e => e.CreatedAt < now && (e.ResolvedAt == null || e.ResolvedAt >= windowStartUtc), ct)).ToList();


        var result = new List<DailyServiceStatus>();

        foreach (var service in Enum.GetValues<ServiceType>())
        {
            var serviceEvents = events.Where(e => e.ServiceType == service).ToList();
            var dayPoints = new List<DailyStatusPoint>(days);

            for (var offset = days - 1; offset >= 0; offset--)
            {
                var date = today.AddDays(-offset);
                var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue),
                    DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);

                var overlapping = serviceEvents.Where(e =>
                    e.CreatedAt < dayEnd &&
                    (e.ResolvedAt == null || e.ResolvedAt > dayStart)).ToList();

                string status;
                if (overlapping.Count == 0)
                    status = PublicServiceState.Healthy;
                else if (overlapping.Any(e => e.Severity == OperationalEventSeverity.Failure))
                    status = PublicServiceState.Down;
                else
                    status = PublicServiceState.Degraded;

                dayPoints.Add(new DailyStatusPoint(date, status));
            }

            result.Add(new DailyServiceStatus(service, dayPoints));
        }

        return result;
    }
}