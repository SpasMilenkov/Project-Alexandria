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
}