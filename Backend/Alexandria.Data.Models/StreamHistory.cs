namespace Alexandria.Data.Models;

public class StreamHistory : IBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;

    public long PositionSeconds { get; set; }
    public long MaxPositionReachedSeconds { get; set; }
    public long TotalListenedSeconds { get; set; }
    public int TimesCompleted { get; set; }
    public DateTime? LastCompletedAt { get; set; }

    public ICollection<StreamSession> Sessions { get; set; } = [];

    public DateTime LastAccessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool HasCompleted => TimesCompleted > 0;
}