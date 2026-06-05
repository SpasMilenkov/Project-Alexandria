namespace Alexandria.Data.Models;

public sealed class DirectoryPolicy : IBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Guid DirectoryId { get; set; }
    public bool InheritedByChildren { get; set; }

    public Directory Directory { get; set; } = null!;
    public ICollection<PolicyRule> Rules { get; set; } = [];
}