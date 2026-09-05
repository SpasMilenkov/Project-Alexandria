using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

/// <summary>
/// Version-level preview work item. The generic <see cref="Job"/> owns the
/// lifecycle (status, retries, timestamps); this row identifies the worker
/// input the way <see cref="TranspilationJob"/> does for transpilation.
/// The <see cref="Preview"/> rows stay success-records only.
/// </summary>
public class PreviewJob : IBase
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid VersionId { get; set; }
    public FileVersion Version { get; set; } = null!;
    public PreviewKind Kind { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}