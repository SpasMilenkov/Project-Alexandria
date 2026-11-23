namespace Alexandria.Dto.Notes;

/// <summary>One summary per requested note, in request order.</summary>
public sealed record NoteSummariesResponse(IReadOnlyList<NoteSummaryDto> Summaries);
