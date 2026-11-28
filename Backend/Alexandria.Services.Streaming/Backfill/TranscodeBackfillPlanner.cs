using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Services.Streaming.Backfill;

/// <summary>What one file needs so its current version carries every rung a Transcode rule asks for.</summary>
internal static class TranscodeBackfillPlanner
{
    public static TranscodeBackfillStep Plan(TranscodeParameters rule, ExistingTranscode? existing)
    {
        if (existing is null)
            return new TranscodeBackfillStep { Action = TranscodeBackfillAction.Create };

        if (existing.Status is JobStatus.Queued or JobStatus.Processing or JobStatus.CancellationRequested)
            return new TranscodeBackfillStep { Action = TranscodeBackfillAction.InFlight };

        // Cancelling was the owner's call, so a rule sweep doesn't undo it
        if (existing.Status == JobStatus.Cancelled)
            return new TranscodeBackfillStep { Action = TranscodeBackfillAction.Cancelled };

        // Measured against the rungs the job asked for, not the ones it produced: the encoder
        // drops rungs above the source, and those would otherwise be requeued on every sweep.
        if (existing.IsVideo)
            return PlanVideo(rule, existing);

        return PlanAudio(rule, existing);
    }

    private static TranscodeBackfillStep PlanVideo(TranscodeParameters rule, ExistingTranscode existing)
    {
        var missing = rule.VideoRungs.Except(existing.VideoRungs).Any();

        if (!missing)
            return Unchanged(existing);

        return new TranscodeBackfillStep
        {
            Action = TranscodeBackfillAction.Requeue,
            VideoRungs = existing.VideoRungs.Union(rule.VideoRungs).Order().ToArray(),
        };
    }

    private static TranscodeBackfillStep PlanAudio(TranscodeParameters rule, ExistingTranscode existing)
    {
        var missing = rule.AudioRungs.Except(existing.AudioRungs).Any();

        if (!missing)
            return Unchanged(existing);

        return new TranscodeBackfillStep
        {
            Action = TranscodeBackfillAction.Requeue,
            AudioRungs = existing.AudioRungs.Union(rule.AudioRungs).Order().ToArray(),
        };
    }

    // A failed or partial job that already asked for every rung stays as it is. Sweeps run on every
    // policy and rule edit across the whole scope, so retrying here would re-encode every broken file
    // each time; a retry stays the owner's call from the job itself.
    private static TranscodeBackfillStep Unchanged(ExistingTranscode existing)
    {
        if (existing.Status == JobStatus.Ready)
            return new TranscodeBackfillStep { Action = TranscodeBackfillAction.Covered };

        return new TranscodeBackfillStep { Action = TranscodeBackfillAction.Unfinished };
    }
}
