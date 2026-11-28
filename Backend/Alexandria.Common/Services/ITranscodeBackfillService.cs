using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface ITranscodeBackfillService
{
    /// <summary>
    /// Gives the files that predate the cutoff, in the folders the rule's policy governs, the
    /// rungs the live Transcode rule asks for. Untranscoded versions get a new job; finished ones
    /// missing a rung, or failed, are requeued with the union of rungs. Running and cancelled
    /// transcodes are left alone. Missing rules or policies and disabled streaming are skipped.
    /// </summary>
    Task<TranscodeBackfillResult> BackfillRuleAsync(TranscodeBackfillRequest request, CancellationToken ct = default);
}
