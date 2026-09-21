using Alexandria.Common.Exceptions.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Shuffle;

public sealed partial class ShuffleSessionStore : IDisposable
{
    internal static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(24);
    internal const int MaxSessionsPerOwner = 8;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<ShuffleSessionStore> _logger;
    private readonly Timer _cleanupTimer;
    private readonly Dictionary<Guid, SessionEntry> _sessions = new();
    private readonly Dictionary<(Guid OwnerId, Guid RequestId), Guid> _requestIndex = new();

    private readonly Dictionary<(Guid OwnerId, Guid RequestId), (string Fingerprint, Task<ShuffleSessionState> Task)>
        _inFlight = new();

    private bool _disposed;

    private sealed class SessionEntry(ShuffleSessionState state, DateTime lastAccessedUtc)
    {
        public ShuffleSessionState State { get; } = state;
        public DateTime LastAccessedUtc { get; set; } = lastAccessedUtc;
    }

    public ShuffleSessionStore(TimeProvider timeProvider, ILogger<ShuffleSessionStore> logger)
    {
        _time = timeProvider;
        _logger = logger;
        _cleanupTimer = new Timer(_ => SweepExpired(), null, CleanupInterval, CleanupInterval);
    }

    internal bool TryGet(Guid sessionId, Guid ownerId, out ShuffleSessionState? state, out DateTime lastAccessedUtc)
    {
        var now = Now();
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var entry)
                && entry.State.OwnerId == ownerId
                && !IsExpired(entry.LastAccessedUtc, now))
            {
                entry.LastAccessedUtc = now;
                state = entry.State;
                lastAccessedUtc = now;
                return true;
            }

            state = null;
            lastAccessedUtc = default;
            return false;
        }
    }

    internal async Task<ShuffleSessionState> GetOrCreateAsync(
        Guid ownerId,
        Guid requestId,
        string fingerprint,
        Func<Task<ShuffleSessionState>> factory,
        CancellationToken ct)
    {
        Task<ShuffleSessionState>? pending;
        var key = (ownerId, requestId);
        var now = Now();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_requestIndex.TryGetValue(key, out var sessionId)
                && _sessions.TryGetValue(sessionId, out var entry)
                && entry.State.OwnerId == ownerId
                && !IsExpired(entry.LastAccessedUtc, now))
            {
                if (entry.State.RequestFingerprint != fingerprint)
                    throw new ShuffleRequestConflictException(requestId);
                entry.LastAccessedUtc = now;
                pending = null;
                return entry.State;
            }

            _requestIndex.Remove(key);

            if (_inFlight.TryGetValue(key, out var inFlight))
            {
                if (inFlight.Fingerprint != fingerprint)
                    throw new ShuffleRequestConflictException(requestId);
                pending = inFlight.Task;
            }
            else
            {
                pending = factory();
                _inFlight[key] = (fingerprint, pending);
                pending.ContinueWith(
                    static (t, s) =>
                    {
                        var (store, inFlightKey) = ((ShuffleSessionStore, (Guid, Guid)))s!;
                        lock (store._gate)
                        {
                            if (store._inFlight.TryGetValue(inFlightKey, out var current)
                                && ReferenceEquals(current.Task, t))
                                store._inFlight.Remove(inFlightKey);
                        }
                    },
                    (this, key),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        var state = await pending.WaitAsync(ct).ConfigureAwait(false);
        lock (_gate)
        {
            PublishInternal(state, Now());
        }

        return state;
    }

    internal bool Remove(Guid sessionId, Guid ownerId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry) || entry.State.OwnerId != ownerId)
                return false;

            _sessions.Remove(sessionId);
            RemoveRequestIndexEntries(sessionId);
            return true;
        }
    }

    private void PublishInternal(ShuffleSessionState state, DateTime now)
    {
        if (_sessions.TryGetValue(state.SessionId, out var existing))
        {
            existing.LastAccessedUtc = now;
            _requestIndex[(state.OwnerId, state.RequestId)] = state.SessionId;
            return;
        }

        _sessions[state.SessionId] = new SessionEntry(state, now);
        _requestIndex[(state.OwnerId, state.RequestId)] = state.SessionId;
        EvictOverflow(state.OwnerId);
    }

    private void EvictOverflow(Guid ownerId)
    {
        while (true)
        {
            Guid? oldestId = null;
            var oldestAccess = default(DateTime);
            var count = 0;

            foreach (var (id, entry) in _sessions)
            {
                if (entry.State.OwnerId != ownerId)
                    continue;

                count++;
                if (oldestId is null
                    || entry.LastAccessedUtc < oldestAccess
                    || (entry.LastAccessedUtc == oldestAccess && id.CompareTo(oldestId.Value) < 0))
                {
                    oldestId = id;
                    oldestAccess = entry.LastAccessedUtc;
                }
            }

            if (count <= MaxSessionsPerOwner || oldestId is null)
                return;

            _sessions.Remove(oldestId.Value);
            RemoveRequestIndexEntries(oldestId.Value);
            LogSessionEvicted(oldestId.Value, ownerId);
        }
    }

    private void RemoveRequestIndexEntries(Guid sessionId)
    {
        foreach (var key in _requestIndex
                     .Where(kv => kv.Value == sessionId)
                     .Select(kv => kv.Key)
                     .ToList())
            _requestIndex.Remove(key);
    }

    private void SweepExpired()
    {
        try
        {
            var now = Now();
            lock (_gate)
            {
                if (_disposed)
                    return;

                var removed = 0;
                foreach (var (id, entry) in _sessions.ToList())
                {
                    if (!IsExpired(entry.LastAccessedUtc, now))
                        continue;
                    _sessions.Remove(id);
                    RemoveRequestIndexEntries(id);
                    removed++;
                }

                if (removed > 0)
                    LogSessionsSwept(removed);
            }
        }
        catch (ObjectDisposedException ex)
        {
            LogSweepFailed(ex);
        }
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;

    private static bool IsExpired(DateTime lastAccessedUtc, DateTime now) =>
        now - lastAccessedUtc > IdleTimeout;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _cleanupTimer.Dispose();
    }
}