using Alexandria.Data.Models;

namespace Alexandria.Dto.Policies;

public sealed class DirectoryPolicyDto
{
    public Guid Id { get; init; }
    public Guid DirectoryId { get; init; }
    public bool InheritedByChildren { get; init; }
    public IEnumerable<PolicyRuleDto> Rules { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static DirectoryPolicyDto FromEntity(DirectoryPolicy policy) => new()
    {
        Id = policy.Id,
        DirectoryId = policy.DirectoryId,
        InheritedByChildren = policy.InheritedByChildren,
        Rules = policy.Rules.Select(PolicyRuleDto.FromEntity),
        CreatedAt = policy.CreatedAt,
        UpdatedAt = policy.UpdatedAt,
    };
}