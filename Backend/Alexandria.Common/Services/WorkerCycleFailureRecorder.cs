using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Extensions;

namespace Alexandria.Common.Services;

/// <summary>
/// Records a worker-cycle failure as a deduplicated operational event so
/// poison-message parking and worker faults surface in monitoring instead of
/// log spam. Shared by all workers; prefer this over worker-local copies.
/// </summary>
public static class WorkerCycleFailureRecorder
{
    public static async Task RecordAsync(
        IUnitOfWork unitOfWork,
        ServiceType serviceType,
        string serviceInstance,
        Exception ex,
        CancellationToken ct)
    {
        // Recurrences keep the open incident alive; it auto-expires once failures stop.
        var touched = await unitOfWork.OperationalEvents.TouchActiveEventAsync(
            serviceType, OperationalEventCode.WorkerCycleFailure, DateTime.UtcNow, ct);

        if (touched > 0)
            return;

        await unitOfWork.OperationalEvents.AddActiveIfAbsentAsync(new OperationalEvent
        {
            Id = Guid.NewGuid(),
            ServiceType = serviceType,
            Code = OperationalEventCode.WorkerCycleFailure,
            Severity = OperationalEventSeverity.Failure,
            Status = OperationalEventStatus.Active,
            MetadataJson = OperationalEventExtensions.SerializeMetadata(
                OperationalEventCode.WorkerCycleFailure,
                new WorkerCycleFailureMetadata
                {
                    ExceptionMessage = ex.Message,
                    ServiceInstance = serviceInstance
                }),
            CreatedBy = SystemConfig.SystemId
        }, ct);
    }
}