using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions.Streaming;

public sealed class OverviewSummaryNotFoundException(Guid userId, SummaryKind kind, DateTime periodStart)
    : Exception($"{kind} summary starting {periodStart:O} was not found for user {userId}.");
