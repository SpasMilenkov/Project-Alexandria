namespace Alexandria.Dto.Audit;

public class ActivityStatisticsOverview
{
    public required int TotalActivity { get; set; }

    /// <summary>Keyed by UTC day, serialized as "yyyy-MM-dd".</summary>
    public required Dictionary<DateOnly, ActivitySummary> ActivityPerDay { get; set; } = [];
}
