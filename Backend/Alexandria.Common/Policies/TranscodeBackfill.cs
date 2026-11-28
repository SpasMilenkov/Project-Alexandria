namespace Alexandria.Common.Policies;

/// <summary>Messaging contract for sweeping a Transcode rule over the files that predate it.</summary>
public static class TranscodeBackfill
{
    /// <summary>
    /// Routing key for JSON backfill requests carrying policy id, rule id, and cutoff. It stays
    /// outside the <c>transpilation.#</c> binding, whose consumer only accepts job ids.
    /// </summary>
    public const string BackfillRoutingKey = "policy.transcode-backfill";
}
