namespace Alexandria.Dto.Audit;

/// <summary>
/// Account and sign-in statistics for the admin over a trailing window. Built only from
/// account and security rows, so no library content is involved.
/// </summary>
public sealed class AccessOverview
{
    public required int WindowDays { get; init; }

    /// <summary>Live accounts that signed in at least once inside the window.</summary>
    public required int ActiveAccounts { get; init; }

    public required int TotalAccounts { get; init; }
    public required int LiveSessions { get; init; }
    public required int LockedAccounts { get; init; }
    public required int FailedSignIns { get; init; }

    /// <summary>Failed sign-ins in the window of the same length just before this one.</summary>
    public required int FailedSignInsPrevious { get; init; }

    public required IReadOnlyList<AccountAccessSummary> Accounts { get; init; }
    public required IReadOnlyList<FailedSignInReasonCount> FailedByReason { get; init; }
    public required IReadOnlyList<FailedSignInSource> FailedSources { get; init; }
    public required IReadOnlyList<UnknownAccountAttempt> UnknownAccountAttempts { get; init; }
    public required IReadOnlyList<AccessNetwork> Networks { get; init; }
}
