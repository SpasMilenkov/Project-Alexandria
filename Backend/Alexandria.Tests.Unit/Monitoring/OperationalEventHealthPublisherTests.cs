using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class OperationalEventHealthPublisherTests
{
    private readonly IOperationalEventRepository _repo = Substitute.For<IOperationalEventRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<OperationalEvent> _added = [];
    private readonly OperationalEventHealthPublisher _sut;

    public OperationalEventHealthPublisherTests()
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

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _sut = new OperationalEventHealthPublisher(scopeFactory);
    }

    private static HealthReportEntry Entry(
        HealthStatus status,
        IEnumerable<string>? tags = null,
        Exception? exception = null,
        string? description = null) =>
        new(status, description, TimeSpan.FromMilliseconds(5), exception, null, tags ?? []);

    private static HealthReport Report(params (string Name, HealthReportEntry Entry)[] entries) =>
        new(entries.ToDictionary(e => e.Name, e => e.Entry), TimeSpan.FromMilliseconds(10));

    [Fact]
    public async Task healthy_check_resolves_unhealthy_and_unreachable_actives()
    {
        // ResolveIfActive only fires when a matching active event exists
        _repo.GetActiveEventAsync(
                Arg.Any<ServiceType>(),
                Arg.Any<OperationalEventCode>(),
                Arg.Any<CancellationToken>())
            .Returns(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                ServiceType = ServiceType.Api,
                Code = OperationalEventCode.HealthcheckUnhealthy,
                Severity = OperationalEventSeverity.Failure,
                Status = OperationalEventStatus.Active,
                CreatedBy = SystemConfig.SystemId
            });
        var report = Report(("postgres", Entry(HealthStatus.Healthy)));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        await _repo.Received(1).ResolveActiveEventsAsync(
            ServiceType.Api, OperationalEventCode.HealthcheckUnhealthy,
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).ResolveActiveEventsAsync(
            ServiceType.Api, OperationalEventCode.HealthcheckUnreachable,
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task unhealthy_infrastructure_check_creates_unhealthy_event_for_api()
    {
        var report = Report(("postgres", Entry(HealthStatus.Unhealthy, tags: ["infrastructure"])));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        var evt = _added.Should().ContainSingle().Subject;
        evt.ServiceType.Should().Be(ServiceType.Api);
        evt.Code.Should().Be(OperationalEventCode.HealthcheckUnhealthy);
        evt.Severity.Should().Be(OperationalEventSeverity.Failure);
        evt.Status.Should().Be(OperationalEventStatus.Active);
        evt.CreatedBy.Should().Be(SystemConfig.SystemId);
    }

    [Fact]
    public async Task worker_name_maps_to_service_type()
    {
        var cases = new (string CheckName, ServiceType Expected)[]
        {
            ("worker-mediaworker", ServiceType.MediaPreviews),
            ("worker-documentworker", ServiceType.DocumentPreviews),
            ("worker-transpilationworker", ServiceType.Transpilation),
            ("worker-lyricsworker", ServiceType.Lyrics),
            ("worker-mediametadataworker", ServiceType.MediaMetadata),
        };

        foreach (var (checkName, expectedService) in cases)
        {
            _added.Clear();
            var report = Report((checkName, Entry(HealthStatus.Degraded, exception: new HttpRequestException("boom"))));

            await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

            _added.Should().ContainSingle($"check '{checkName}' should map to {expectedService}");
            _added[0].ServiceType.Should().Be(expectedService);
        }
    }

    [Fact]
    public async Task http_exception_maps_to_unreachable_code()
    {
        var report = Report(("worker-lyricsworker",
            Entry(HealthStatus.Unhealthy, exception: new HttpRequestException("connection refused"))));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        _added.Single().Code.Should().Be(OperationalEventCode.HealthcheckUnreachable);
    }

    [Fact]
    public async Task timeout_exception_maps_to_unreachable_code()
    {
        var report = Report(("worker-transpilationworker",
            Entry(HealthStatus.Unhealthy, exception: new TaskCanceledException("timed out"))));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        _added.Single().Code.Should().Be(OperationalEventCode.HealthcheckUnreachable);
    }

    [Fact]
    public async Task other_exceptions_map_to_unhealthy_code()
    {
        var report = Report(("storage",
            Entry(HealthStatus.Unhealthy, exception: new InvalidOperationException("disk on fire"))));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        _added.Single().Code.Should().Be(OperationalEventCode.HealthcheckUnhealthy);
    }

    [Fact]
    public async Task no_duplicate_event_when_active_already_exists()
    {
        _repo.GetActiveEventAsync(
                Arg.Any<ServiceType>(),
                Arg.Any<OperationalEventCode>(),
                Arg.Any<CancellationToken>())
            .Returns(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                ServiceType = ServiceType.Api,
                Code = OperationalEventCode.HealthcheckUnhealthy,
                Severity = OperationalEventSeverity.Failure,
                Status = OperationalEventStatus.Active,
                CreatedBy = SystemConfig.SystemId
            });

        var report = Report(("postgres", Entry(HealthStatus.Unhealthy, tags: ["infrastructure"])));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        _added.Should().BeEmpty();
        await _repo.DidNotReceive().ResolveActiveEventsAsync(
            Arg.Any<ServiceType>(),
            Arg.Any<OperationalEventCode>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task failure_metadata_serializes_instance_and_detail()
    {
        var report = Report(("rabbitmq",
            Entry(HealthStatus.Unhealthy, description: "broker unreachable")));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(_added.Single().MetadataJson!);
        var root = doc.RootElement;

        root.GetProperty("kind").GetString().Should().Be("healthcheck-failure");
        root.GetProperty("checkName").GetString().Should().Be("rabbitmq");
        root.GetProperty("serviceInstance").GetString().Should().Be("rabbitmq");
        root.GetProperty("detail").GetString().Should().Be("broker unreachable");
    }

    [Fact]
    public async Task unknown_check_names_are_ignored()
    {
        var report = Report(("mystery-check", Entry(HealthStatus.Unhealthy)));

        await _sut.PublishAsync(report, TestContext.Current.CancellationToken);

        _added.Should().BeEmpty();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}