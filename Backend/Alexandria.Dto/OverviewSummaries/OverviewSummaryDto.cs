using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.OverviewSummaries;

public sealed class OverviewSummaryDto
{
    public Guid Id { get; init; }
    public SummaryKind Kind { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public DateTime GeneratedAt { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public bool IsFinal { get; init; }
    public int SchemaVersion { get; init; }
    public required SummaryPayload Payload { get; init; }
}

public sealed class OverviewSummaryHeaderDto
{
    public Guid Id { get; init; }
    public SummaryKind Kind { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public DateTime GeneratedAt { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public bool IsFinal { get; init; }
    public int SchemaVersion { get; init; }
}
