using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Policies;

public static class PolicyRuleTrigger
{
    public static bool Matches(PolicyTriggerType triggerType, string triggerValue, string mimeType)
    {
        return triggerType switch
        {
            PolicyTriggerType.AnyFile => true,
            PolicyTriggerType.FileGroup => FileGroups.MatchesMimeType(triggerValue, mimeType),
            _ => false
        };
    }
}
