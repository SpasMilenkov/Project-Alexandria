namespace Alexandria.Dto.Enrichment;

/// <summary>
/// One backfill candidate: a live, predating-policy file with no successful enrichment
/// job yet. Per-file enqueue still applies the MIME + already-enriched idempotency
/// checks, so this stays coarse on purpose.
/// </summary>
public sealed record EnrichmentBackfillCandidate(Guid FileId, string MimeType);

/// <summary>
/// Backfill outcome for one policy. Skipped covers unsupported MIME types and files
/// that gained a successful enrichment between candidate selection and enqueue.
/// </summary>
public sealed record EnrichmentBackfillResult(int Candidates, int Published, int Skipped);
