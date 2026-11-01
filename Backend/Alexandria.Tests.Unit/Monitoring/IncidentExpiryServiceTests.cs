using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Services.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class IncidentExpiryServiceTests
{
    private readonly IOperationalEventService _events = Substitute.For<IOperationalEventService>();

    private IncidentExpiryService CreateSut(IncidentExpiryOptions options)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOperationalEventService)).Returns(_events);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new IncidentExpiryService(scopeFactory, Options.Create(options),
            NullLogger<IncidentExpiryService>.Instance);
    }

    [Fact]
    public async Task sweep_expires_each_code_with_its_own_idle_window()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(new IncidentExpiryOptions { WorkerCycleFailureIdleMinutes = 30, UserReportIdleHours = 24 });

        await sut.SweepAsync(now, TestContext.Current.CancellationToken);

        await _events.Received(1).ExpireIdleIncidentsAsync(
            OperationalEventCode.WorkerCycleFailure, now.AddMinutes(-30), Arg.Any<CancellationToken>());
        await _events.Received(1).ExpireIdleIncidentsAsync(
            OperationalEventCode.UserReportedProblem, now.AddHours(-24), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task sweep_clamps_non_positive_windows_to_minimum()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(new IncidentExpiryOptions { WorkerCycleFailureIdleMinutes = 0, UserReportIdleHours = -5 });

        await sut.SweepAsync(now, TestContext.Current.CancellationToken);

        await _events.Received(1).ExpireIdleIncidentsAsync(
            OperationalEventCode.WorkerCycleFailure, now.AddMinutes(-1), Arg.Any<CancellationToken>());
        await _events.Received(1).ExpireIdleIncidentsAsync(
            OperationalEventCode.UserReportedProblem, now.AddHours(-1), Arg.Any<CancellationToken>());
    }
}
