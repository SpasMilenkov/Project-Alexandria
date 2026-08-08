namespace Alexandria.Workers.MediaMetadata.Queueing;

/// <summary>
/// Queue of file IDs whose enrichment just committed and which should be auto-tagged.
/// Worker-local (in-process) counterpart of <c>IPromotionQueue</c>; consumed only by the
/// <c>AutoTagSyncWorker</c> drainer.
/// </summary>
public interface IAutoTagQueue
{
    ValueTask QueueFileAsync(Guid fileId, CancellationToken ct = default);
}