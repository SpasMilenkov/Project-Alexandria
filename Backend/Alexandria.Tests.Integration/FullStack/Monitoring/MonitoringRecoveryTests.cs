using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Status;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Monitoring;

public class MonitoringRecoveryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static OperationalEvent ActiveEvent(OperationalEventCode code, ServiceType service) => new()
    {
        Id = Guid.NewGuid(),
        ServiceType = service,
        Code = code,
        Severity = OperationalEventSeverity.Failure,
        Status = OperationalEventStatus.Active,
        CreatedBy = SystemConfig.SystemId
    };

    [Fact]
    public async Task AddActiveIfAbsent_second_active_incident_for_same_code_is_rejected()
    {
        using var firstScope = Factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        using var secondScope = Factory.Services.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var inserted = await first.OperationalEvents.AddActiveIfAbsentAsync(
            ActiveEvent(OperationalEventCode.WorkerCycleFailure, ServiceType.Lyrics),
            TestContext.Current.CancellationToken);
        var duplicate = await second.OperationalEvents.AddActiveIfAbsentAsync(
            ActiveEvent(OperationalEventCode.WorkerCycleFailure, ServiceType.Lyrics),
            TestContext.Current.CancellationToken);

        inserted.Should().BeTrue();
        duplicate.Should().BeFalse();
    }

    [Fact]
    public async Task User_reports_are_not_deduplicated_by_the_active_index()
    {
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var first = await unitOfWork.OperationalEvents.AddActiveIfAbsentAsync(
            ActiveEvent(OperationalEventCode.UserReportedProblem, ServiceType.Api),
            TestContext.Current.CancellationToken);
        var second = await unitOfWork.OperationalEvents.AddActiveIfAbsentAsync(
            ActiveEvent(OperationalEventCode.UserReportedProblem, ServiceType.Api),
            TestContext.Current.CancellationToken);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public async Task Public_status_history_cache_varies_by_days()
    {
        var oneDay = await Anon.GetFromJsonAsync<List<DailyServiceStatus>>("/api/status/history?days=1",
            TestContext.Current.CancellationToken);
        var sevenDays = await Anon.GetFromJsonAsync<List<DailyServiceStatus>>("/api/status/history?days=7",
            TestContext.Current.CancellationToken);

        oneDay!.Should().OnlyContain(s => s.Days.Count == 1);
        sevenDays!.Should().OnlyContain(s => s.Days.Count == 7);
    }

    [Fact]
    public async Task Requeue_job_requires_admin()
    {
        var response = await Auth.PostAsync($"/api/admin/jobs/{Guid.NewGuid()}/requeue", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Requeue_unknown_job_returns_not_found_and_ready_job_conflicts()
    {
        var admin = Factory.CreateAuthenticatedClient(UserId, "Admin");
        var readyJobId = Guid.NewGuid();

        using (var scope = Factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.Jobs.AddAsync(new Job
            {
                Id = readyJobId,
                Type = JobType.Transpilation,
                Status = JobStatus.Ready,
                UserId = UserId,
                CreatedAt = DateTime.UtcNow
            }, TestContext.Current.CancellationToken);
        }

        var missing = await admin.PostAsync($"/api/admin/jobs/{Guid.NewGuid()}/requeue", null,
            TestContext.Current.CancellationToken);
        var ready = await admin.PostAsync($"/api/admin/jobs/{readyJobId}/requeue", null,
            TestContext.Current.CancellationToken);

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ready.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
