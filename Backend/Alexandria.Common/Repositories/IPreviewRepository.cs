using Alexandria.Data.Models;
using Alexandria.Dto.PreviewsStats;

namespace Alexandria.Common.Repositories;

public interface IPreviewRepository : IRepository<Preview>
{
    public Task<Preview> CreateAsync(Preview file, CancellationToken ct = default);
    public Task<Preview> UpdateAsync(Preview file, CancellationToken ct = default);

    /// <summary>Current all-time artifact count and size total per kind.</summary>
    Task<IReadOnlyList<PreviewKindTotals>> GetKindTotalsAsync(CancellationToken ct = default);

    /// <summary>Artifacts created inside the window, no tracking — volume bucketing input.</summary>
    Task<IReadOnlyList<Preview>> GetCreatedBetweenAsync(
        DateTime from, DateTime to, CancellationToken ct = default);
}