using System.Linq.Expressions;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Files;
using Alexandria.Dto.Status;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class OperationalEventServiceTests
{
    private readonly IOperationalEventRepository _repo = Substitute.For<IOperationalEventRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly OperationalEventService _sut;

    public OperationalEventServiceTests()
    {
        _unitOfWork.OperationalEvents.Returns(_repo);
        _sut = new OperationalEventService(_unitOfWork);
    }

    [Fact]
    public async Task get_events_delegates_query_to_repository()
    {
        var query = new OperationalEventQuery(ServiceType.Lyrics, null, null, null, null, null, 2, 50);
        var expected = new PaginatedResult<OperationalEvent>();
        _repo.GetEventsAsync(query, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetEventsAsync(query, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task get_error_counts_delegates_range_to_repository()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var expected = new List<ErrorAggregate>
        {
            new(DateOnly.FromDateTime(from), ServiceType.Api, OperationalEventSeverity.Failure, 3)
        };
        _repo.GetErrorCountsAsync(from, to, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetErrorCountsAsync(from, to, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task get_uptime_delegates_arguments_to_repository()
    {
        _repo.CalculateUptimeAsync(ServiceType.Transpilation,
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(99.5);

        var result = await _sut.GetUptimeAsync(ServiceType.Transpilation,
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, TestContext.Current.CancellationToken);

        await _repo.Received(1).CalculateUptimeAsync(
            ServiceType.Transpilation,
            Arg.Is<DateTime>(d => d <= DateTime.UtcNow),
            Arg.Is<DateTime>(d => d <= DateTime.UtcNow),
            Arg.Any<CancellationToken>());
        result.Should().Be(99.5);
    }

    [Fact]
    public async Task get_current_status_delegates_to_repository()
    {
        var expected = new List<OperationalEvent>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ServiceType = ServiceType.Api,
                Code = OperationalEventCode.HealthcheckUnhealthy,
                Severity = OperationalEventSeverity.Failure,
                Status = OperationalEventStatus.Active,
                CreatedBy = SystemConfig.SystemId
            }
        };
        _repo.GetCurrentStatusPerServiceAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetCurrentStatusAsync(TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task report_problem_creates_active_partial_failure_event_on_api_service()
    {
        OperationalEvent? captured = null;
        _repo.AddAsync(Arg.Do<OperationalEvent>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<OperationalEvent>(0));

        var userId = Guid.NewGuid();

        var result = await _sut.ReportProblemAsync(userId, "Uploads stall at 90%",
            "/my-storage", TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.ServiceType.Should().Be(ServiceType.Api);
        captured.Code.Should().Be(OperationalEventCode.UserReportedProblem);
        captured.Severity.Should().Be(OperationalEventSeverity.PartialFailure);
        captured.Status.Should().Be(OperationalEventStatus.Active);
        captured.CreatedBy.Should().Be(userId);
        result.Should().BeSameAs(captured);
    }

    [Fact]
    public async Task report_problem_serializes_user_reported_metadata()
    {
        OperationalEvent? captured = null;
        _repo.AddAsync(Arg.Do<OperationalEvent>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<OperationalEvent>(0));

        await _sut.ReportProblemAsync(Guid.NewGuid(), "Search returns stale results",
            "pageContext-value", TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(captured!.MetadataJson!);
        var root = doc.RootElement;

        root.GetProperty("kind").GetString().Should().Be("user-reported");
        root.GetProperty("serviceInstance").GetString().Should().Be("user-report");
        root.GetProperty("description").GetString().Should().Be("Search returns stale results");
        root.GetProperty("pageContext").GetString().Should().Be("pageContext-value");
    }

    [Fact]
    public async Task report_problem_persists_via_unit_of_work()
    {
        _repo.AddAsync(Arg.Any<OperationalEvent>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<OperationalEvent>(0));

        await _sut.ReportProblemAsync(Guid.NewGuid(), "Something broke", null,
            TestContext.Current.CancellationToken);

        await _repo.Received(1).AddAsync(Arg.Any<OperationalEvent>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task resolve_active_event_stamps_resolution_and_appends_note()
    {
        var evt = new OperationalEvent
        {
            Id = Guid.NewGuid(),
            ServiceType = ServiceType.Api,
            Code = OperationalEventCode.UserReportedProblem,
            Severity = OperationalEventSeverity.PartialFailure,
            Status = OperationalEventStatus.Active,
            MetadataJson =
                """{"kind":"user-reported","serviceInstance":"user-report","description":"broken"}""",
            CreatedBy = SystemConfig.SystemId
        };
        _repo.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);
        var resolvedBy = Guid.NewGuid();

        var result = await _sut.ResolveAsync(evt.Id, resolvedBy, "Restarted the worker",
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ResolveOutcome.Resolved);
        result.Event.Should().BeSameAs(evt);
        evt.Status.Should().Be(OperationalEventStatus.Resolved);
        evt.ResolvedAt.Should().NotBeNull();
        evt.UpdatedAt.Should().NotBeNull();
        evt.UpdatedBy.Should().Be(resolvedBy);

        using var doc = JsonDocument.Parse(evt.MetadataJson!);
        var root = doc.RootElement;
        root.GetProperty("kind").GetString().Should().Be("user-reported");
        root.GetProperty("resolutionNote").GetString().Should().Be("Restarted the worker");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task resolve_skips_metadata_append_when_note_is_empty()
    {
        const string original = """{"kind":"healthcheck-failure","checkName":"postgres"}""";
        var evt = new OperationalEvent
        {
            Id = Guid.NewGuid(),
            ServiceType = ServiceType.Api,
            Code = OperationalEventCode.HealthcheckUnhealthy,
            Severity = OperationalEventSeverity.Failure,
            Status = OperationalEventStatus.Active,
            MetadataJson = original,
            CreatedBy = SystemConfig.SystemId
        };
        _repo.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);

        var result = await _sut.ResolveAsync(evt.Id, Guid.NewGuid(), "   ",
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ResolveOutcome.Resolved);
        evt.MetadataJson.Should().Be(original);
    }

    [Fact]
    public async Task resolve_already_resolved_event_returns_conflict_outcome_without_saving()
    {
        var evt = new OperationalEvent
        {
            Id = Guid.NewGuid(),
            ServiceType = ServiceType.Api,
            Code = OperationalEventCode.HealthcheckUnhealthy,
            Severity = OperationalEventSeverity.Failure,
            Status = OperationalEventStatus.Resolved,
            CreatedBy = SystemConfig.SystemId
        };
        _repo.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(evt);

        var result = await _sut.ResolveAsync(evt.Id, Guid.NewGuid(), null,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ResolveOutcome.AlreadyResolved);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task resolve_missing_event_returns_not_found()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OperationalEvent?)null);

        var result = await _sut.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), null,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ResolveOutcome.NotFound);
        result.Event.Should().BeNull();
    }

    private static OperationalEvent Event(
        ServiceType service,
        OperationalEventSeverity severity,
        DateTime createdAt,
        DateTime? resolvedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ServiceType = service,
            Code = OperationalEventCode.HealthcheckUnhealthy,
            Severity = severity,
            Status = resolvedAt is null
                ? OperationalEventStatus.Active
                : OperationalEventStatus.Resolved,
            ResolvedAt = resolvedAt,
            CreatedAt = createdAt,
            CreatedBy = SystemConfig.SystemId
        };

    [Fact]
    public async Task status_history_ongoing_failure_marks_subsequent_days_down()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Failure started on the second window day at a fixed hour, never
        // resolved → that day and every later day Down; the first window day
        // (before creation) stays Healthy.
        var createdAt = DateTime.SpecifyKind(
            today.AddDays(-2).ToDateTime(TimeOnly.Parse("08:00")), DateTimeKind.Utc);
        var evt = Event(ServiceType.Api, OperationalEventSeverity.Failure, createdAt, null);
        _repo.FindAsync(Arg.Any<Expression<
                Func<OperationalEvent, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([evt]);

        var result = await _sut.GetPublicStatusHistoryAsync(4, TestContext.Current.CancellationToken);

        var api = result.Single(r => r.ServiceType == ServiceType.Api);
        api.Days.Should().HaveCount(4);
        api.Days[0].Status.Should().Be(PublicServiceState.Healthy);
        api.Days[1].Status.Should().Be(PublicServiceState.Down);
        api.Days[2].Status.Should().Be(PublicServiceState.Down);
        api.Days[3].Status.Should().Be(PublicServiceState.Down);
        api.Days[3].Date.Should().Be(today);
    }

    [Fact]
    public async Task status_history_midday_resolution_splits_adjacent_days()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        // Failed during yesterday, resolved mid-afternoon yesterday → yesterday
        // Down; nothing overlaps today → Healthy.
        var failedAt = DateTime.SpecifyKind(
            yesterday.ToDateTime(TimeOnly.Parse("10:00")), DateTimeKind.Utc);
        var resolvedAt = DateTime.SpecifyKind(
            yesterday.ToDateTime(TimeOnly.Parse("16:00")), DateTimeKind.Utc);
        var evt = Event(ServiceType.Lyrics, OperationalEventSeverity.Failure, failedAt, resolvedAt);
        _repo.FindAsync(Arg.Any<Expression<
                Func<OperationalEvent, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([evt]);

        var result = await _sut.GetPublicStatusHistoryAsync(2, TestContext.Current.CancellationToken);

        var lyrics = result.Single(r => r.ServiceType == ServiceType.Lyrics);
        lyrics.Days[0].Date.Should().Be(yesterday);
        lyrics.Days[0].Status.Should().Be(PublicServiceState.Down);
        lyrics.Days[1].Date.Should().Be(today);
        lyrics.Days[1].Status.Should().Be(PublicServiceState.Healthy);
    }

    [Fact]
    public async Task status_history_degraded_only_day_is_degraded_not_down()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var evt = Event(ServiceType.MediaPreviews, OperationalEventSeverity.PartialFailure,
            DateTime.UtcNow.AddHours(-6), null);
        _repo.FindAsync(Arg.Any<Expression<
                Func<OperationalEvent, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([evt]);

        var result = await _sut.GetPublicStatusHistoryAsync(1, TestContext.Current.CancellationToken);

        var previews = result.Single(r => r.ServiceType == ServiceType.MediaPreviews);
        previews.Days.Single().Status.Should().Be(PublicServiceState.Degraded);
    }

    [Fact]
    public async Task status_history_covers_every_service_in_order()
    {
        _repo.FindAsync(Arg.Any<Expression<
                Func<OperationalEvent, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OperationalEvent>());

        var result = await _sut.GetPublicStatusHistoryAsync(7, TestContext.Current.CancellationToken);

        result.Should().HaveCount(Enum.GetValues<ServiceType>().Length);
        result.Select(r => r.ServiceType).Should().Equal(
            ServiceType.Api,
            ServiceType.MediaPreviews,
            ServiceType.DocumentPreviews,
            ServiceType.Transpilation,
            ServiceType.Lyrics,
            ServiceType.MediaMetadata);
        result.Should().OnlyContain(r => r.Days.Count == 7);
        result.Should().OnlyContain(r =>
            r.Days.All(d => d.Status == PublicServiceState.Healthy));
    }
}