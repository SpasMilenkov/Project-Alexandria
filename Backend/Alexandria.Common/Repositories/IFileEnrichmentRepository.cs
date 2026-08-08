using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IFileEnrichmentRepository : IRepository<FileEnrichment>
{
    /// <summary>
    /// Inserts <paramref name="row"/> or, when a row with the same
    /// <c>(FileId, Analyzer, Version)</c> already exists (unique index), updates its
    /// <c>PayloadJson</c>/audit columns instead. Idempotent by design.
    /// </summary>
    Task<FileEnrichment> UpsertAsync(FileEnrichment row, CancellationToken ct = default);

    /// <summary>
    /// Returns true when the file has at least one non-deleted enrichment row whose payload
    /// is a successful result (i.e. not a <c>{"success":false,...}</c> failure row). Used by
    /// the auto-tag trigger to avoid re-publishing enrichment for already-enriched files.
    /// </summary>
    Task<bool> HasSuccessfulAutoTagEnrichmentAsync(Guid fileId, CancellationToken ct = default);
}