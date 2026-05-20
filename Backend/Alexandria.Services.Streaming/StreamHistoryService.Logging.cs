using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class StreamHistoryService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching stream history for file {FileId}, user {UserId}")]
    private partial void LogFetchingHistory(Guid fileId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listing stream history for user {UserId}")]
    private partial void LogFindingHistory(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting stream session for file {FileId}, user {UserId}")]
    private partial void LogStartingSession(Guid fileId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Created new stream history {HistoryId} for file {FileId}, user {UserId}")]
    private partial void LogCreatedHistory(Guid historyId, Guid fileId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} started under history {HistoryId}")]
    private partial void LogSessionStarted(Guid sessionId, Guid historyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Closing session {SessionId} for user {UserId}")]
    private partial void LogClosingSession(Guid sessionId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Session {SessionId} reached completion threshold for history {HistoryId}")]
    private partial void LogSessionCompleted(Guid sessionId, Guid historyId);
}