namespace Alexandria.Dto.Audit;

public sealed class AccountAccessSummary
{
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }

    /// <summary>Latest sign-in ever recorded, so a quiet account still shows when it was last used.</summary>
    public DateTimeOffset? LastSignInAt { get; init; }

    public required int SignIns { get; init; }

    /// <summary>Addresses this account signed in from for the first time inside the window.</summary>
    public required int NewNetworks { get; init; }

    public required int FailedSignIns { get; init; }
    public required int LiveSessions { get; init; }

    /// <summary>Raw user agents of the live sessions, one per distinct agent.</summary>
    public required IReadOnlyList<string> SessionUserAgents { get; init; }

    public DateTimeOffset? LockoutEnd { get; init; }
}
