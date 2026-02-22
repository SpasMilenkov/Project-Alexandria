namespace Common.Exceptions;

public class UserCreationException(string message, Dictionary<string, List<string>> errors)
    : Exception(message)
{
    public Dictionary<string, List<string>> Errors { get; } = errors;
}
