using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Backfill;

public sealed partial class TranscodeBackfillService
{
    [LoggerMessage(5100, LogLevel.Information,
        "Skipping transcode backfill for policy {PolicyId}: adaptive streaming is disabled")]
    private static partial void LogStreamingDisabled(ILogger logger, Guid policyId);

    [LoggerMessage(5101, LogLevel.Information,
        "Skipping transcode backfill: policy {PolicyId} no longer exists")]
    private static partial void LogPolicyNotFound(ILogger logger, Guid policyId);

    [LoggerMessage(5102, LogLevel.Information,
        "Skipping transcode backfill: rule {RuleId} is no longer a live Transcode rule of policy {PolicyId}")]
    private static partial void LogRuleNotFound(ILogger logger, Guid ruleId, Guid policyId);

    [LoggerMessage(5103, LogLevel.Debug,
        "Transcode backfill skipped file {FileId}: its transcode started or changed during the sweep")]
    private static partial void LogAlreadyInFlight(ILogger logger, Guid fileId);

    [LoggerMessage(5104, LogLevel.Information,
        "Transcode backfill for rule {RuleId} of policy {PolicyId}: {Candidates} candidates, {Created} new jobs, {Requeued} requeued")]
    private static partial void LogBackfillCompleted(ILogger logger, Guid ruleId, Guid policyId,
        int candidates, int created, int requeued);

    [LoggerMessage(5105, LogLevel.Warning,
        "Transcode backfill skipped file {FileId}: version {VersionId} has no live content to transcode")]
    private static partial void LogContentMissing(ILogger logger, Guid fileId, Guid versionId);
}
