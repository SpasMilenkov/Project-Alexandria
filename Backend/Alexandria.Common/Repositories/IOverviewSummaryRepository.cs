using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Common.Summaries;
using Alexandria.Dto.OverviewSummaries;

namespace Alexandria.Common.Repositories;

public interface IOverviewSummaryRepository : IRepository<OverviewSummary>
{
    Task<OverviewSummary?> GetByPeriodAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default);

    Task<IReadOnlyList<OverviewSummaryHeaderDto>> ListHeadersAsync(
        Guid userId, SummaryKind kind, DateTime? from, DateTime? to, bool? finalOnly,
        CancellationToken ct = default);

    Task<UpsertOutcome> UpsertAsync(OverviewSummary candidate, CancellationToken ct = default);

    /// <summary>
    /// Deletes all provisional rows for the user and kind. Finalized rows are never touched.
    /// </summary>
    Task<int> DeleteAllProvisionalAsync(Guid userId, SummaryKind kind, CancellationToken ct = default);
}
