using System.Text.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Policies;

public sealed class PolicyRuleDto
{
    public Guid Id { get; init; }
    public Guid PolicyId { get; init; }
    public PolicyActionType ActionType { get; init; }
    public PolicyTriggerType TriggerType { get; init; }
    public string TriggerValue { get; init; } = null!;
    public int Priority { get; init; }
    public bool ApplyOnNewVersion { get; init; }
    public JsonElement Parameters { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static PolicyRuleDto FromEntity(PolicyRule rule) => new()
    {
        Id = rule.Id,
        PolicyId = rule.PolicyId,
        ActionType = rule.ActionType,
        TriggerType = rule.TriggerType,
        TriggerValue = rule.TriggerValue,
        Priority = rule.Priority,
        ApplyOnNewVersion = rule.ApplyOnNewVersion,
        Parameters = rule.Parameters.RootElement.Clone(),
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt,
    };
}