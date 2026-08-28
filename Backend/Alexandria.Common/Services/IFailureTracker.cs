using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Common.Services;

/// <summary>
/// This is a temporary hack to track failures until I properly
/// rework the Job table to handle all types of jobs and
/// account for errors, retries and causation properly.
/// Until then this counts errors over a sliding window in memory
/// It is prone to breaking every time the worker using it is reset
/// because the count will be lost :(
/// </summary>
public interface IJobOutcomeTracker
{
    void RecordSuccess(ServiceType serviceType);
    void RecordFailure(ServiceType serviceType);
    (int Failures, int Total) GetCountsSince(ServiceType serviceType, DateTime since);
}