using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class EssentiaBatchFile : IBase
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public EssentiaBatch Batch { get; set; } = null!;

    public Guid FileId { get; set; }

    public File File { get; set; } = null!;

    public EssentiaBatchFileStatus Status { get; set; } = EssentiaBatchFileStatus.Pending;

    public string? ErrorDetail { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}