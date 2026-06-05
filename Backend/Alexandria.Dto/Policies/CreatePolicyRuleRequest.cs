using System.Text.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Policies;

public sealed class CreatePolicyRuleRequest
{
    public PolicyActionType ActionType { get; init; }
    public PolicyTriggerType TriggerType { get; init; }
    public string TriggerValue { get; init; } = null!;
    public int Priority { get; init; }
    public bool ApplyOnNewVersion { get; init; }
    public JsonDocument Parameters { get; init; } = null!;

    public PolicyRule ToEntity(Guid policyId, Guid updatedBy) => new()
    {
        PolicyId = policyId,
        ActionType = ActionType,
        TriggerType = TriggerType,
        TriggerValue = TriggerValue,
        Priority = Priority,
        ApplyOnNewVersion = ApplyOnNewVersion,
        Parameters = Parameters,
        UpdatedBy = updatedBy,
    };
}