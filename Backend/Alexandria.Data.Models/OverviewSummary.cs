using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

/// <summary>
/// A per-user, per-period aggregate snapshot. Provisional while FinalizedAt is null,
/// frozen for good once it is set. The payload is stored as JSON; typing lives in
/// the Dto layer to preserve project reference direction.
/// </summary>
public class OverviewSummary : IBase
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public SummaryKind Kind { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public int SchemaVersion { get; set; }

    public required string PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
