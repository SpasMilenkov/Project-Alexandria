using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class Job : IBase
{
    public Guid Id { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public int ProgressPercent { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorDetail { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public JobType Type { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}