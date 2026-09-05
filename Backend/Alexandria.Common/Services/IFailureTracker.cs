using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Services;

/// <summary>
/// Reports job success/failure counts over a sliding time window, used by the
/// failure-rate threshold checker to detect degraded job workers. Backed by the
/// Job table; a job's outcome is whatever UpdateStatusAsync last persisted for it.
/// </summary>
public interface IJobOutcomeTracker
{
    Task<(int Failures, int Total)> GetCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default);
}