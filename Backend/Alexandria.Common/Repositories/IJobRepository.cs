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

    Task<IReadOnlyList<Job>> GetStalledJobsAsync(TimeSpan threshold, CancellationToken ct = default);

    /// <summary>Current all-time job count per status, optionally filtered to one job type.</summary>
    Task<IReadOnlyList<JobStatusCount>> GetStatusCountsAsync(
        JobType? type = null, CancellationToken ct = default);

    Task<(int Failures, int Total)> GetOutcomeCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default);

    Task<PaginatedResult<Job>> FindJobsAsync(JobQuery query, CancellationToken ct = default);

    Task UpdateStatusForJobsAsync(
        IEnumerable<Guid> jobIds, JobStatus status, string? errorDetail = null, CancellationToken ct = default);

    Task<IReadOnlyList<Job>> GetJobsTouchingWindowAsync(DateTime from, DateTime to, CancellationToken ct);
}