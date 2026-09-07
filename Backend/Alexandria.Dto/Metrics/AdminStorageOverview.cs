namespace Alexandria.Dto.Metrics;

public class AdminStorageOverview
{
    public required StorageInfo Capacity { get; set; }

    /// <summary>Sum of designated quotas over live accounts, in bytes.</summary>
    public long TotalAssignedQuotaBytes { get; set; }

    public int TotalUsers { get; set; }
}