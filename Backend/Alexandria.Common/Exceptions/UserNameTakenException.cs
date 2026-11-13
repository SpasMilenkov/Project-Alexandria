namespace Alexandria.Common.Exceptions;

public class UserNameTakenException(string userName)
    : InvalidOperationException($"The username '{userName}' is already taken.")
{
    public string UserName { get; } = userName;
}
