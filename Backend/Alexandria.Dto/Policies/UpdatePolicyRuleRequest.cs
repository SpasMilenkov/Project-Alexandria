using System.Text.Json;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Policies;

public sealed class UpdatePolicyRuleRequest
{
    public PolicyTriggerType TriggerType { get; init; }
    public string TriggerValue { get; init; } = null!;
    public int Priority { get; init; }
    public bool ApplyOnNewVersion { get; init; }
    public JsonDocument Parameters { get; init; } = null!;
}