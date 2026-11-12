namespace Alexandria.Dto.Audit;

/// <summary>Failed sign-ins for an email that matches no account.</summary>
public sealed class UnknownAccountAttempt
{
    public required string Email { get; init; }
    public required int Count { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}
