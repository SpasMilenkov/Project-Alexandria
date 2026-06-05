using Alexandria.Data.Models;

namespace Alexandria.Dto.Policies;

public sealed class CreateDirectoryPolicyRequest
{
    public Guid DirectoryId { get; init; }
    public bool InheritedByChildren { get; init; }

    public DirectoryPolicy ToEntity(Guid updatedBy) => new()
    {
        DirectoryId = DirectoryId,
        InheritedByChildren = InheritedByChildren,
        UpdatedBy = updatedBy,
    };
}