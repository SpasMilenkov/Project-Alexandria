namespace Alexandria.Common.Exceptions;

public class UserNotFoundException(Guid userId)
    : KeyNotFoundException($"User {userId} not found.")
{
    public Guid UserId { get; } = userId;
}
