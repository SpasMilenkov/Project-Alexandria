namespace Alexandria.Dto.Audit;

public sealed class SecuritySummary
{
    public required IReadOnlyList<AuditLogResult> RecentLogins { get; init; }
    public required IReadOnlyList<AuditLogResult> RecentFailedAttempts { get; init; }
    public required int FailedAttemptsLast30Days { get; init; }
    public required IReadOnlyList<KnownIpAddress> KnownIpAddresses { get; init; }
}
