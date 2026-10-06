using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Wrapped;

public sealed partial class OverviewSummaryService
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Serving frozen {Kind} summary for user {UserId}, period starting {PeriodStart}")]
    private partial void LogServingFrozen(SummaryKind kind, Guid userId, DateTime periodStart);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Serving live cached {Kind} summary for user {UserId}, period starting {PeriodStart}")]
    private partial void LogServingLiveCached(SummaryKind kind, Guid userId, DateTime periodStart);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Finalize skipped, {Kind} summary for user {UserId}, period starting {PeriodStart} is already frozen")]
    private partial void LogAlreadyFrozen(SummaryKind kind, Guid userId, DateTime periodStart);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Upsert of {Kind} summary for user {UserId}, period starting {PeriodStart}: {Outcome} (finalize: {Finalize})")]
    private partial void LogUpserted(SummaryKind kind, Guid userId, DateTime periodStart, UpsertOutcome outcome, bool finalize);
}
