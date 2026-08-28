using System.Collections.Concurrent;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Services.Monitoring;

public class JobOutcomeTracker(TimeProvider? timeProvider = null) : IJobOutcomeTracker
{
    // Injectable for tests; production resolves to the real clock
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<ServiceType, ConcurrentQueue<(DateTime Time, bool Failed)>> _outcomes = new();

    public void RecordSuccess(ServiceType serviceType) => Record(serviceType, failed: false);
    public void RecordFailure(ServiceType serviceType) => Record(serviceType, failed: true);

    private void Record(ServiceType serviceType, bool failed)
    {
        var queue = _outcomes.GetOrAdd(serviceType, _ => new ConcurrentQueue<(DateTime, bool)>());
        queue.Enqueue((_time.GetUtcNow().UtcDateTime, failed));
    }

    public (int Failures, int Total) GetCountsSince(ServiceType serviceType, DateTime since)
    {
        if (!_outcomes.TryGetValue(serviceType, out var queue))
            return (0, 0);

        while (queue.TryPeek(out var oldest) && oldest.Time < since)
            queue.TryDequeue(out _);

        var relevant = queue.Where(o => o.Time >= since).ToList();
        return (relevant.Count(o => o.Failed), relevant.Count);
    }
}