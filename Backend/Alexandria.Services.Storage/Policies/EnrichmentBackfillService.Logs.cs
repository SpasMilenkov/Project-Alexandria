using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Policies;

public sealed partial class EnrichmentBackfillService
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Backfill skipped for policy {PolicyId}: policy not found or deleted.")]
    private static partial void LogPolicyNotFound(ILogger logger, Guid policyId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Backfill skipped for policy {PolicyId}: autotagging is disabled.")]
    private static partial void LogBackfillSkippedDisabled(ILogger logger, Guid policyId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Backfill for policy {PolicyId} published {Published}/{Candidates} enrich trigger(s).")]
    private static partial void LogBackfillCompleted(ILogger logger, Guid policyId, int candidates, int published);
}