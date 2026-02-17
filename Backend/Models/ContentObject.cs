namespace Models;

public class ContentObject : IBase
{
    public Guid Id { get; set; }
    public required byte[] Hash { get; set; } = [];
    public required string StorageKey { get; set; }
    public bool IsPromoted { get; set; } = false;
    public DateTime? PromotedAt { get; set; }
    public int PromotionAttempts { get; set; } = 0;
    public DateTime? LastPromotionAttemptAt { get; set; }
    public DateTime? OrphanedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Guid? UploadId { get; set; }
    public Upload? Upload { get; set; }
}