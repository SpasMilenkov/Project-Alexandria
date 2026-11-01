using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Jobs;

namespace Alexandria.Common.Services;

public interface IJobRequeueService
{
    /// <summary>
    /// Requeues one Failed, Cancelled or stalled Processing job through the pipeline that owns its
    /// type, restoring its automatic retry budget.
    /// </summary>
    Task<JobRequeueOutcome> RequeueAsync(Guid jobId, CancellationToken ct);

    /// <summary>
    /// Requeues up to <paramref name="limit"/> Failed jobs of one type.
    /// </summary>
    /// <returns>The number of jobs requeued.</returns>
    Task<int> RequeueFailedAsync(JobType type, int limit, CancellationToken ct);
}
