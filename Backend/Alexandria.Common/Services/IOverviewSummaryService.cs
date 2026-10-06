using Alexandria.Data.Models.Enumerators;
using Alexandria.Common.Summaries;
using Alexandria.Dto.OverviewSummaries;

namespace Alexandria.Common.Services;

public interface IOverviewSummaryService
{
    Task<IReadOnlyList<OverviewSummaryHeaderDto>> ListAsync(
        Guid userId, SummaryKind kind, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<OverviewSummaryDto> GetAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default);

    Task<OverviewSummaryDto> FinalizeAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default);
}
