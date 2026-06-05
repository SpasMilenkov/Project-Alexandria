using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class PolicyDispatcher
{
    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to resolve policy for directory {DirectoryId} on file {FileId}.")]
    private static partial void LogPolicyResolutionFailed(ILogger logger, Exception ex, Guid directoryId, Guid fileId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Queued {ActionType} job for file {FileId} via rule {RuleId}.")]
    private static partial void LogJobQueued(ILogger logger, string actionType, Guid fileId, Guid ruleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispatch rule {RuleId} for file {FileId}.")]
    private static partial void LogRuleDispatchFailed(ILogger logger, Exception ex, Guid ruleId, Guid fileId);
}