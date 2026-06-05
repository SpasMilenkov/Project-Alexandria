namespace Alexandria.Common.Exceptions.Policies;

public sealed class PolicyRuleNotFoundException(Guid ruleId)
    : Exception($"Policy rule {ruleId} was not found.");