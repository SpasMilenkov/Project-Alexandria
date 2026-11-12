namespace Alexandria.Dto.Audit;

/// <summary>First and last sign-in of one account from one address, and how many there were.</summary>
public sealed record AuditSignInHistoryRow(
    Guid? AccountId,
    string? IpAddress,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    int SignIns);
