using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Shuffle;

public sealed partial class ShuffleSessionStore
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Evicted shuffle session {SessionId} for owner {OwnerId} after session limit")]
    private partial void LogSessionEvicted(Guid sessionId, Guid ownerId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Swept {Count} expired shuffle sessions")]
    private partial void LogSessionsSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Shuffle session expiry sweep ran against a disposed store")]
    private partial void LogSweepFailed(Exception exception);
}