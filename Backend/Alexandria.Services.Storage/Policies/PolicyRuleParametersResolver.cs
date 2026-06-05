using System.Text.Json;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Policies;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;

namespace Alexandria.Services.Storage.Policies;

public static class PolicyRuleParametersResolver
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Resolves parameters from a raw <see cref="PolicyRule"/> entity.</summary>
    public static IPolicyRuleParameters Resolve(PolicyRule rule)
        => Deserialize(rule.ActionType, rule.Parameters.RootElement, rule.Id);

    /// <summary>Resolves parameters from a <see cref="PolicyRuleDto"/> (used in the dispatcher).</summary>
    public static IPolicyRuleParameters ResolveFromDto(PolicyRuleDto rule)
        => Deserialize(rule.ActionType, rule.Parameters, rule.Id);

    private static IPolicyRuleParameters Deserialize(
        PolicyActionType actionType,
        JsonElement element,
        Guid ruleId)
    {
        return actionType switch
        {
            PolicyActionType.Transcode => DeserializeAs<TranscodeParameters>(element, ruleId),
            PolicyActionType.Backup => DeserializeAs<BackupParameters>(element, ruleId),
            PolicyActionType.AutoTag => DeserializeAs<AutoTagParameters>(element, ruleId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(actionType), actionType, "Unknown policy action type")
        };
    }

    private static T DeserializeAs<T>(JsonElement element, Guid ruleId)
        where T : IPolicyRuleParameters
    {
        try
        {
            return element.Deserialize<T>(Options)
                   ?? throw new InvalidPolicyParametersException(ruleId);
        }
        catch (JsonException ex)
        {
            throw new InvalidPolicyParametersException(ruleId, ex);
        }
    }
}