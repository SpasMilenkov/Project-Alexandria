namespace Alexandria.Data.Models;

public class StreamHistory : IBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;

    // stored in seconds, sufficient granularity for resume
    public long PositionSeconds { get; set; }
    public bool Completed { get; set; }

    public DateTime LastAccessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}