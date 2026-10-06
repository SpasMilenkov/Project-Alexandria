using Alexandria.Data.Models.Enumerators;
using Alexandria.Common.Summaries;
using Alexandria.Dto.OverviewSummaries;

namespace Alexandria.Common.Services;

public interface ISummaryGenerator
{
    SummaryKind Kind { get; }

    int SchemaVersion { get; }

    Task<SummaryPayload> GenerateAsync(Guid userId, SummaryPeriod period, CancellationToken ct = default);
}
