using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions.Streaming;

public sealed class SummaryPeriodNotClosedException(SummaryKind kind, SummaryPeriod period)
    : Exception($"Cannot finalize {kind} summary: period ending {period.End:O} has not closed yet.");
