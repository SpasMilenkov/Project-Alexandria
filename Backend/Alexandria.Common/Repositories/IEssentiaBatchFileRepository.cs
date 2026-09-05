using Alexandria.Data.Models;
using Alexandria.Dto.Enrichment;

namespace Alexandria.Common.Repositories;

public interface IEssentiaBatchFileRepository : IRepository<EssentiaBatchFile>
{
    /// <summary>
    /// Returns the batch-files for the given batch.
    /// </summary>
    Task<IEnumerable<EssentiaBatchFile>> GetByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Auto-tag sweep backstop: distinct, non-deleted file IDs whose newest enrichment
    /// outcome was a success (<c>JobStatus.Ready</c>) but which have no
    /// <c>Auto</c>-source <c>FileTag</c> at all. Covers files whose enrichment committed
    /// but whose tag sync was lost (worker crash between commit and enqueue). A file that
    /// has any <c>Auto</c> tag is assumed already synced — partial facet coverage is
    /// legitimate (e.g. a facet the model couldn't tag), so it is not re-queued.
    /// </summary>
    Task<IEnumerable<Guid>> GetAutoTagBackstopCandidatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Auto-tag sweep retry candidates: distinct, non-deleted file IDs whose newest
    /// enrichment attempt (any outcome) is a <c>Failed</c> linked job
    /// that is older than <paramref name="attemptCutoff"/> (the retry cooldown), and whose
    /// current version is not client-encrypted. Encrypted files can never be enriched, so
    /// they are never re-queued.
    /// </summary>
    Task<IEnumerable<Guid>> GetAutoTagRetryCandidatesAsync(DateTime attemptCutoff, CancellationToken ct = default);

    /// <summary>
    /// Live queue depth: batch-files belonging to non-terminal (dispatched) batches,
    /// counted per (backbone, file status).
    /// </summary>
    Task<IReadOnlyList<EnrichmentQueueDepthDto>> GetQueueDepthAsync(CancellationToken ct = default);

    /// <summary>
    /// Batch-files still <c>Queued</c> inside a dispatched batch that were created before
    /// <paramref name="olderThan"/> — candidates for the admin "stuck" panel.
    /// </summary>
    Task<IReadOnlyList<EnrichmentStuckDto>> GetStuckAsync(DateTime olderThan, CancellationToken ct = default);

    /// <summary>
    /// Terminal batch-file outcomes between <paramref name="from"/> (inclusive) and
    /// <paramref name="to"/> (exclusive), bucketed per backbone via Postgres
    /// <c>date_trunc</c>. See <see cref="EnrichmentFailureRatePointDto"/> for the rate
    /// definition.
    /// </summary>
    Task<IReadOnlyList<EnrichmentFailureRatePointDto>> GetFailureRateAsync(
        DateTime from,
        DateTime to,
        EnrichmentBucket bucket,
        CancellationToken ct = default);

    /// <summary>
    /// Per-backbone avg/median/max duration (seconds) over terminal batch-files. Median via
    /// raw <c>PERCENTILE_CONT(0.5)</c> — interpolated SQL, never client-side eval.
    /// </summary>
    Task<IReadOnlyList<EnrichmentDurationDto>> GetDurationAsync(CancellationToken ct = default);

    /// <summary>
    /// Batch-file job counts per whole-hour bucket of <c>CreatedAt</c> between
    /// <paramref name="from"/> (inclusive) and <paramref name="to"/> (exclusive).
    /// </summary>
    Task<IReadOnlyList<EnrichmentVolumePointDto>> GetVolumeByHourAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}