using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class StreamHistoryService
{
    [LoggerMessage(7000, LogLevel.Debug,
        "Upserted playback position for user {UserId} on file {FileId}: {PositionSeconds}s, completed={Completed}")]
    private static partial void LogPositionUpserted(
        ILogger logger, Guid userId, Guid fileId, long positionSeconds, bool completed);
}