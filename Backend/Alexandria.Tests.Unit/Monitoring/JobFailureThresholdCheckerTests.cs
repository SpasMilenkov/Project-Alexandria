using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class JobFailureThresholdCheckerTests
{
    private readonly IOperationalEventRepository _repo = Substitute.For<IOperationalEventRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJobOutcomeTracker _tracker = Substitute.For<IJobOutcomeTracker>();
    private readonly List<OperationalEvent> _added = [];

    private readonly OperationalEvent _activeEvent = new()
    {
        Id = Guid.NewGuid(),
        ServiceType = Monitored,
        Code = OperationalEventCode.ErrorRateThresholdExceeded,
        Severity = OperationalEventSeverity.DegradedPerformance,
        Status = OperationalEventStatus.Active,
        CreatedBy = SystemConfig.SystemId
    };

    private static readonly ServiceType Monitored = ServiceType.Lyrics;

    private JobFailureThresholdChecker CreateSut()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new JobFailureThresholdChecker(_tracker, scopeFactory, Monitored,
            Substitute.For<ILogger<JobFailureThresholdChecker>>());
    }

    public JobFailureThresholdCheckerTests()
    {
        _unitOfWork.OperationalEvents.Returns(_repo);
        _repo.AddAsync(Arg.Do<OperationalEvent>(e => _added.Add(e)), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<OperationalEvent>(0));
        _repo.ResolveActiveEventsAsync(
                Arg.Any<ServiceType>(),
                Arg.Any<OperationalEventCode>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(1);
    }

    private void SeedOutcomes(int failures, int successes)
    {
        _tracker.GetCountsSinceAsync(
                Arg.Any<JobType>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((failures, failures + successes));
    }

    [Fact]
    public async Task below_minimum_sample_size_resolves_existing_event_instead_of_judging_rate()
    {
        SeedOutcomes(failures: 9, successes: 0);
        _repo.GetActiveEventAsync(Monitored, OperationalEventCode.ErrorRateThresholdExceeded,
                Arg.Any<CancellationToken>())
            .Returns(_activeEvent);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        await _repo.Received(1).ResolveActiveEventsAsync(
            Monitored, OperationalEventCode.ErrorRateThresholdExceeded,
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task below_minimum_sample_size_is_noop_when_service_is_clean()
    {
        SeedOutcomes(failures: 4, successes: 5);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        _added.Should().BeEmpty();
        await _repo.DidNotReceive().ResolveActiveEventsAsync(
            Arg.Any<ServiceType>(),
            Arg.Any<OperationalEventCode>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task failure_rate_at_threshold_does_not_open_event()
    {
        // 3 / 20 = exactly the 15% threshold; only a strictly higher rate opens
        SeedOutcomes(failures: 3, successes: 17);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task rate_above_threshold_opens_exactly_one_event_even_when_rerun()
    {
        // First run: no active event exists. Second run: one does.
        SeedOutcomes(failures: 8, successes: 3);
        _repo.GetActiveEventAsync(Monitored, OperationalEventCode.ErrorRateThresholdExceeded,
                Arg.Any<CancellationToken>())
            .Returns(null as OperationalEvent, _activeEvent);
        var sut = CreateSut();

        await sut.CheckAsync(TestContext.Current.CancellationToken);
        await sut.CheckAsync(TestContext.Current.CancellationToken);

        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task recovery_resolves_active_event_when_rate_drops()
    {
        SeedOutcomes(failures: 1, successes: 29); // 1/30 ≈ 3%
        _repo.GetActiveEventAsync(Monitored, OperationalEventCode.ErrorRateThresholdExceeded,
                Arg.Any<CancellationToken>())
            .Returns(_activeEvent);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        await _repo.Received(1).ResolveActiveEventsAsync(
            Monitored, OperationalEventCode.ErrorRateThresholdExceeded,
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task checker_queries_the_mapped_job_type_for_the_monitored_service()
    {
        SeedOutcomes(failures: 8, successes: 3);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        await _tracker.Received(1).GetCountsSinceAsync(
            JobType.LyricsFetch, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task created_event_carries_threshold_metadata_and_system_actor()
    {
        SeedOutcomes(failures: 8, successes: 3);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        var evt = _added.Should().ContainSingle().Subject;
        evt.ServiceType.Should().Be(Monitored);
        evt.Code.Should().Be(OperationalEventCode.ErrorRateThresholdExceeded);
        evt.Severity.Should().Be(OperationalEventSeverity.DegradedPerformance);
        evt.Status.Should().Be(OperationalEventStatus.Active);
        evt.CreatedBy.Should().Be(SystemConfig.SystemId);

        using var doc = JsonDocument.Parse(evt.MetadataJson!);
        var root = doc.RootElement;

        root.GetProperty("kind").GetString().Should().Be("error-rate-threshold");
        root.GetProperty("serviceInstance").GetString().Should().Be("Lyrics");
        root.GetProperty("failureCount").GetInt32().Should().Be(8);
        root.GetProperty("windowMinutes").GetInt32().Should().Be(120);
        root.GetProperty("threshold").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task every_check_cycle_saves_changes()
    {
        SeedOutcomes(failures: 0, successes: 10);

        await CreateSut().CheckAsync(TestContext.Current.CancellationToken);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}