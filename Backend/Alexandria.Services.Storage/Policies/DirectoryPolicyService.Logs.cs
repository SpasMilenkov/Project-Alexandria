using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class DirectoryPolicyService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Policy {PolicyId} created for directory {DirectoryId}.")]
    private static partial void LogPolicyCreated(ILogger logger, Guid policyId, Guid directoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create policy for directory {DirectoryId}.")]
    private static partial void LogPolicyCreateFailed(ILogger logger, Exception ex, Guid directoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Policy {PolicyId} updated.")]
    private static partial void LogPolicyUpdated(ILogger logger, Guid policyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Policy {PolicyId} deleted.")]
    private static partial void LogPolicyDeleted(ILogger logger, Guid policyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete policy {PolicyId}.")]
    private static partial void LogPolicyDeleteFailed(ILogger logger, Exception ex, Guid policyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rule {RuleId} added to policy {PolicyId}.")]
    private static partial void LogRuleAdded(ILogger logger, Guid ruleId, Guid policyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rule {RuleId} updated.")]
    private static partial void LogRuleUpdated(ILogger logger, Guid ruleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rule {RuleId} deleted.")]
    private static partial void LogRuleDeleted(ILogger logger, Guid ruleId);
}