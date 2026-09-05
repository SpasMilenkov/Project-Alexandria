using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class EssentiaBatch : IBase
{
    public Guid Id { get; set; }

    public EssentiaBackbone Backbone { get; set; }

    public EssentiaBatchStatus Status { get; set; } = EssentiaBatchStatus.Dispatched;
    public DateTime? DispatchedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<EssentiaBatchFile> Files { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}