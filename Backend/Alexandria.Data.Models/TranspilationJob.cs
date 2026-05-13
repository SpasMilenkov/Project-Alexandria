using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class TranspilationJob : IBase
{
    public Guid Id { get; set; }
    public Guid ContentObjectId { get; set; }
    public ContentObject ContentObject { get; set; } = null!;

    public TranspilationStatus Status { get; set; } = TranspilationStatus.Queued;
    public bool IsVideo { get; set; }

    public int ProgressPercent { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorDetail { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When set, results are restricted to jobs whose content object is referenced
    /// by at least one file owned by this user.
    /// Always set this to the calling user's ID in user-facing queries.
    /// </summary>
    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<StreamingRepresentation> Representations { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}