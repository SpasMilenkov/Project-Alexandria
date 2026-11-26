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

    /// <summary>
    /// Records how far a Processing job has come and stamps <c>UpdatedAt</c>, which also serves
    /// as its heartbeat. Leaves the status and <c>StartedAt</c> alone, and does nothing once the
    /// job has left Processing.
    /// </summary>
    Task UpdateProgressAsync(Guid jobId, int percent, CancellationToken ct = default);

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
    /// Resets a Failed or Cancelled job, or one stuck in Processing or Queued since before
    /// <paramref name="stalledBefore"/>, to a fresh Queued state with its retry budget restored.
    /// A job stuck in Queued lost its message on the broker side, for example after a purge.
    /// Returns <see langword="false"/> when the job is in any other state.
    /// </summary>
    Task<bool> TryResetForManualRequeueAsync(Guid jobId, DateTime stalledBefore, CancellationToken ct = default);

    /// <summary>
    /// Atomically moves a finished job (Ready, Partial, Failed or Cancelled) back to a fresh Queued
    /// state for a rerun the user asked for, with its retry budget restored. Returns
    /// <see langword="false"/> while the job is still queued or running, so a rerun can never reopen
    /// a claim a worker holds.
    /// </summary>
    Task<bool> TryRequeueFinishedAsync(Guid jobId, CancellationToken ct = default);

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
    /// Fails the job only while it is still Queued, for a delivery that was given up on before any
    /// worker claimed it. A job a worker already claimed keeps the outcome that worker recorded.
    /// </summary>
    Task<bool> FailIfQueuedAsync(Guid jobId, string errorDetail, CancellationToken ct = default);

    /// <summary>Current all-time job count per status, optionally filtered to one job type.</summary>
    Task<IReadOnlyList<JobStatusCount>> GetStatusCountsAsync(
        JobType? type = null, CancellationToken ct = default);

    Task<(int Failures, int Total)> GetOutcomeCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default);

    Task<PaginatedResult<Job>> FindJobsAsync(JobQuery query, CancellationToken ct = default);

    /// <summary>Read-only admin listing, newest activity first, projected in the database.</summary>
    Task<PaginatedResult<AdminJobDto>> FindAdminJobsAsync(JobQuery query, CancellationToken ct = default);

    Task UpdateStatusForJobsAsync(
        IEnumerable<Guid> jobIds, JobStatus status, string? errorDetail = null, CancellationToken ct = default);

    Task<IReadOnlyList<Job>> GetJobsTouchingWindowAsync(DateTime from, DateTime to, CancellationToken ct,
        JobType? type = null);
}