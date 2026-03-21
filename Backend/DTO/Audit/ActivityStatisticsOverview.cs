namespace DTO.Audit;

public class ActivityStatisticsOverview
{
    public required int TotalActivity { get; set; }
    public required Dictionary<int, ActivitySummary> ActivityPerDay{ get; set; } = [];
}
