namespace Alexandria.Common.Policies;

public static class JobRetryPolicy
{
    // Shared between TryClaimJobAsync (enforces the ceiling) and the polling
    // worker's query (excludes jobs already at the ceiling from the pool).
    public const int MaxAutoRetries = 5;
}