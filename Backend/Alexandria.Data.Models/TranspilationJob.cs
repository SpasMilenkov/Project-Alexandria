using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class TranspilationJob : IBase
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public FileVersion FileVersion { get; set; } = null!;

    public TranspilationStatus Status { get; set; } = TranspilationStatus.Queued;
    public bool IsVideo { get; set; }

    public AudioRung[] AudioRungs { get; set; } = [];
    public VideoRung[] VideoRungs { get; set; } = [];

    public int ProgressPercent { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorDetail { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // relative path root in the streaming bucket, e.g. "{fileId}/v/1080p_av1"
    public string? SegmentPrefix { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<StreamingRepresentation> Representations { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}