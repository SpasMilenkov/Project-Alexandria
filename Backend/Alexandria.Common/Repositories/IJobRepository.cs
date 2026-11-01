using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Jobs;
using Alexandria.Dto.TranspilationStats;

namespace Alexandria.Common.Repositories;

public interface IJobRepository : IRepository<Job>
{
    /// <summary>
    /// Atomically transitions the job from <see cref="JobStatus.Queued"/> to
    /// <see cref="JobStatus.Processing"/> and stamps <c>StartedAt</c>.
    /// Returns <see langword="true"/> when this caller won the claim;
    /// <see langword="false"/> when the job was already claimed or does not exist.
    /// </summary>
    Task<bool> TryClaimJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Returns the status of a job.
    /// </summary>
    Task<JobStatus> GetStatusAsync(Guid jobId, CancellationToken ct = default);

    Task UpdateStatusAsync(
        Guid jobId,
        JobStatus status,
        int? progress = null,
        string? errorDetail = null,
        CancellationToken ct = default);

    Task ClearErrorAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Processing jobs whose last heartbeat (<c>UpdatedAt</c>, falling back to <c>StartedAt</c>)
    /// is older than <paramref name="threshold"/>, optionally restricted to one type.
    /// </summary>
    Task<IReadOnlyList<Job>> GetStalledJobsAsync(TimeSpan threshold, JobType? type = null,
        CancellationToken ct = default);

    /// <summary>
    /// Marks Processing jobs of <paramref name="type"/> whose heartbeat is older than
    /// <paramref name="heartbeatBefore"/> as Failed, so retry and requeue paths can claim them again.
    /// </summary>
    /// <returns>The number of jobs failed.</returns>
    Task<int> FailStalledJobsAsync(JobType type, DateTime heartbeatBefore, string errorDetail,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a claimed job to Queued without counting an attempt, used when a worker stops
    /// mid-job so the broker's redelivery can claim it again.
    /// </summary>
    Task<bool> ReleaseClaimAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Atomically moves a Failed job with retries remaining back to Queued and counts the attempt.
    /// Returns <see langword="false"/> when the job is no longer eligible.
    /// </summary>
    Task<bool> TryRequeueForRetryAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Resets a Failed, Cancelled, or stalled Processing job to a fresh Queued state with its retry
    /// budget restored. Returns <see langword="false"/> when the job is in any other state.
    /// </summary>
    Task<bool> TryResetForManualRequeueAsync(Guid jobId, DateTime stalledBefore, CancellationToken ct = default);

    /// <summary>
    /// Exhausts the automatic retry budget for a job whose failure cannot succeed on retry.
    /// </summary>
    Task MarkNonRetryableAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Failed jobs of the given types that still have automatic retries left and failed before
    /// <paramref name="failedBefore"/>, oldest failure first.
    /// </summary>
    Task<IReadOnlyList<Job>> GetRetryCandidatesAsync(IReadOnlyCollection<JobType> types, DateTime failedBefore,
        int limit, CancellationToken ct = default);

    /// <summary>
    /// Queued jobs of the given types untouched since <paramref name="queuedBefore"/>; their
    /// broker message was most likely lost.
    /// </summary>
    Task<IReadOnlyList<Job>> GetOrphanedQueuedJobsAsync(IReadOnlyCollection<JobType> types, DateTime queuedBefore,
        int limit, CancellationToken ct = default);

    /// <summary>Stamps <c>UpdatedAt</c> so recovery sweeps measure from the latest republish.</summary>
    Task TouchAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Current all-time job count per status, optionally filtered to one job type.</summary>
    Task<IReadOnlyList<JobStatusCount>> GetStatusCountsAsync(
        JobType? type = null, CancellationToken ct = default);

    Task<(int Failures, int Total)> GetOutcomeCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default);

    Task<PaginatedResult<Job>> FindJobsAsync(JobQuery query, CancellationToken ct = default);

    Task UpdateStatusForJobsAsync(
        IEnumerable<Guid> jobIds, JobStatus status, string? errorDetail = null, CancellationToken ct = default);

    Task<IReadOnlyList<Job>> GetJobsTouchingWindowAsync(DateTime from, DateTime to, CancellationToken ct,
        JobType? type = null);
}