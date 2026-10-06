namespace Alexandria.Dto.Files.Streaming.Stats;

public sealed record TimelineDayTop(string Title, string? Artist, long Seconds);

public sealed record TimelineDayPoint(string Date, long Seconds, int Plays, IReadOnlyList<TimelineDayTop> Top)
{
    public int SessionCount { get; init; }
}

public sealed record TimelineMonthPoint(string Date, long Seconds, int Plays)
{
    public int SessionCount { get; init; }
}

public sealed record TimelineResponse(
    DateTime From,
    DateTime To,
    IReadOnlyList<TimelineDayPoint> Days,
    IReadOnlyList<TimelineMonthPoint> Months,
    long TotalSeconds,
    int SessionCount,
    int ActiveDays)
{
    public int QualifiedPlayCount { get; init; }
}
