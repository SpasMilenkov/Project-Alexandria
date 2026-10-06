namespace Alexandria.Common.Summaries;

public readonly record struct SummaryPeriod
{
    public SummaryPeriod(DateTime start, DateTime end)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Summary periods must be expressed in UTC.");
        }

        if (end <= start)
        {
            throw new ArgumentException("Summary period end must be after its start.");
        }

        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public static SummaryPeriod ForYear(int year) => new(
        new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    public bool IsClosedAt(DateTime utcNow) => End <= utcNow;
}
