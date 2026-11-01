using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class WorkerCycleFailureRecorderTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IOperationalEventRepository _events = Substitute.For<IOperationalEventRepository>();

    public WorkerCycleFailureRecorderTests()
    {
        _unitOfWork.OperationalEvents.Returns(_events);
    }

    [Fact]
    public async Task record_recurrence_touches_active_incident_without_inserting()
    {
        _events.TouchActiveEventAsync(ServiceType.Lyrics, OperationalEventCode.WorkerCycleFailure,
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(1);

        await WorkerCycleFailureRecorder.RecordAsync(_unitOfWork, ServiceType.Lyrics, "lyrics",
            new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        await _events.DidNotReceiveWithAnyArgs().AddActiveIfAbsentAsync(default!, default);
    }

    [Fact]
    public async Task record_first_failure_inserts_active_incident()
    {
        await WorkerCycleFailureRecorder.RecordAsync(_unitOfWork, ServiceType.Lyrics, "lyrics",
            new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        await _events.Received(1).AddActiveIfAbsentAsync(
            Arg.Is<OperationalEvent>(e => e.Code == OperationalEventCode.WorkerCycleFailure
                                          && e.ServiceType == ServiceType.Lyrics
                                          && e.Status == OperationalEventStatus.Active),
            Arg.Any<CancellationToken>());
    }
}
