using Alexandria.Data.Models;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IDocumentTextRepository : IRepository<DocumentText>
{
    Task<DocumentText?> GetByContentObjectIdAsync(Guid contentObjectId, CancellationToken ct = default);

    /// <summary>
    /// Inserts the row or replaces the existing row for its content, computing the search vector
    /// in Postgres. When the whole text is too varied to fit one vector, it indexes the first
    /// <see cref="Services.DocumentTextExtractor.FallbackIndexedCharacters"/> characters instead.
    /// Returns true when the row ended up truncated, either way.
    /// Must not run inside a transaction: the retry needs the failed statement rolled back alone.
    /// </summary>
    Task<bool> UpsertAsync(DocumentText text, CancellationToken ct = default);

    /// <summary>
    /// Marks the content as failed at <paramref name="extractorVersion"/>. Text from an earlier
    /// successful extraction stays searchable. Consecutive failures at the same version count up;
    /// a failure at a newer version starts the count again.
    /// </summary>
    Task RecordFailureAsync(Guid contentObjectId, int extractorVersion, string errorDetail,
        CancellationToken ct = default);

    /// <summary>
    /// Promoted content of the given types, held by at least one live, unencrypted version of a
    /// live file, that has no final row from <paramref name="extractorVersion"/> or newer. Failed
    /// rows come back once their last attempt is older than <paramref name="retryFailedBefore"/>,
    /// until they reach <paramref name="maxAttempts"/>. Most recently uploaded first.
    /// </summary>
    Task<IReadOnlyList<DocumentTextCandidate>> GetExtractionCandidatesAsync(
        IReadOnlyCollection<string> mimeTypes,
        int extractorVersion,
        DateTime retryFailedBefore,
        int maxAttempts,
        int limit,
        CancellationToken ct = default);
}