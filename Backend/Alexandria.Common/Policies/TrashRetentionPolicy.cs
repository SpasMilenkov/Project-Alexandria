namespace Alexandria.Common.Policies;

public static class TrashRetentionPolicy
{
    // One window for everything trash-related: restore eligibility, the trash size the
    // user sees, and how long orphaned content survives before the cleanup worker purges
    // it. Diverging values would let a restorable file lose its bytes, or report space
    // that has already been reclaimed.
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public static DateTime RestorableSince(DateTime utcNow) => utcNow - RetentionPeriod;
}
