using Alexandria.Dto.Audit;
using Alexandria.Dto.Users;

namespace Alexandria.Services.Storage.Access;

/// <summary>Everything the overview is computed from, fetched up front so the math stays pure.</summary>
internal sealed class AccessOverviewInput
{
    public required IReadOnlyList<AccountStateRow> Accounts { get; init; }
    public required IReadOnlyList<AccountSessionRow> Sessions { get; init; }

    /// <summary>Sign-ins inside the window.</summary>
    public required IReadOnlyList<AuditSignInRow> SignIns { get; init; }

    /// <summary>
    /// First and last sign-in per account and address over the whole log, so last sign-in and
    /// first-seen dates are not cut off by the window.
    /// </summary>
    public required IReadOnlyList<AuditSignInHistoryRow> History { get; init; }

    /// <summary>Failed sign-ins from the start of the previous window on.</summary>
    public required IReadOnlyList<FailedSignIn> Failures { get; init; }
}
