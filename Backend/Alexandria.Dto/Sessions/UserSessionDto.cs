namespace Alexandria.Dto.Sessions;

public sealed class UserSessionDto
{
    public required Guid Id { get; init; }
    public required DateTime SessionStartedAt { get; init; }

    /// <summary>Tokens rotate on every refresh, so the active token's creation is the last activity.</summary>
    public required DateTime LastActiveAt { get; init; }

    public required DateTime ExpiresAt { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public required bool IsCurrent { get; init; }
}
