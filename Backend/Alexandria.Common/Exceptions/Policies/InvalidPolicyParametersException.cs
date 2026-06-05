namespace Alexandria.Common.Exceptions.Policies;

public sealed class InvalidPolicyParametersException : Exception
{
    public InvalidPolicyParametersException(Guid ruleId)
        : base($"Policy rule {ruleId} has invalid or missing parameters.")
    {
    }

    public InvalidPolicyParametersException(Guid ruleId, Exception inner)
        : base($"Policy rule {ruleId} parameters could not be deserialized.", inner)
    {
    }
}