using Alexandria.Dto.StorageStats;

namespace Alexandria.Common.Services;

public interface IAdminStorageStatsService
{
    /// <summary>Global artifact split over live files (plus trash). No tracking.</summary>
    Task<StorageSplitResponse> GetSplitAsync(CancellationToken ct);

    /// <summary>Top consuming accounts by live usage. Zero-usage accounts excluded.</summary>
    Task<IReadOnlyList<UserStorageRankDto>> GetUserRankingAsync(int top, CancellationToken ct);
}