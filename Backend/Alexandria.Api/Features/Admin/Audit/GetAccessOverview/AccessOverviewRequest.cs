namespace Alexandria.Api.Features.Admin.Audit.GetAccessOverview;

sealed class AccessOverviewRequest
{
    /// <summary>Trailing window in days; the previous window of the same length is used for comparison.</summary>
    public int Days { get; set; } = 30;
}
