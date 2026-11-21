namespace Alexandria.Dto.DocumentTextStats;

/// <summary>
/// How far the text sweep has got across every account's documents.
/// </summary>
/// <param name="StatusCounts">Documents per outcome, for the outcomes that occur.</param>
/// <param name="WaitingCount">
/// Documents the sweep can read but has not yet read with the current extractor.
/// </param>
/// <param name="RecentFailures">The most recent failures, newest first.</param>
/// <param name="MaxAttempts">How many times the sweep tries a document before giving up.</param>
public sealed record DocumentTextOverviewResponse(
    IReadOnlyList<DocumentTextStatusCount> StatusCounts,
    int WaitingCount,
    IReadOnlyList<DocumentTextFailure> RecentFailures,
    int MaxAttempts);