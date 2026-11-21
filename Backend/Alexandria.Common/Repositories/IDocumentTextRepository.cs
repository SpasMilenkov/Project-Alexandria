using Alexandria.Data.Models;
using Alexandria.Dto.DocumentTextStats;
using Alexandria.Dto.Files;

namespace Alexandria.Common.Repositories;

public interface IDocumentTextRepository : IRepository<DocumentText>
{
    Task<DocumentText?> GetByContentObjectIdAsync(Guid contentObjectId, CancellationToken ct = default);

    /// <summary>
    /// Excerpts of the content's text around the words of <paramref name="query"/>, parsed the
    /// same way the file search parses it, plus the text's status. Cuts them in Postgres, which
    /// takes about 120 ms per million characters, so it serves one file at a time.
    /// </summary>
    Task<ContentMatchesResponse> GetContentMatchesAsync(Guid contentObjectId, string query,
        CancellationToken ct = default);

    /// <summary>
    /// One excerpt at the first place the query's words appear, for a one-line search row. It
    /// cuts the excerpt from a short stretch of the text instead of the whole document, which is
    /// about five times faster on long documents, and falls back to
    /// <see cref="GetContentMatchesAsync"/> when that stretch holds no match.
    /// </summary>
    Task<ContentMatchesResponse> GetFirstContentMatchAsync(Guid contentObjectId, string query,
        CancellationToken ct = default);

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
    /// Counts an attempt before the document is read, recorded as a failure that says reading
    /// stopped partway. A document that kills the worker therefore still uses up its attempts
    /// instead of being picked first again after every restart. The attempt then ends with
    /// <see cref="UpsertAsync"/>, <see cref="FailAttemptAsync"/> or <see cref="AbandonAttemptAsync"/>.
    /// </summary>
    Task BeginAttemptAsync(Guid contentObjectId, int extractorVersion, CancellationToken ct = default);

    /// <summary>
    /// Replaces the message of the attempt in progress with why reading failed, without counting
    /// the attempt a second time.
    /// </summary>
    Task FailAttemptAsync(Guid contentObjectId, string errorDetail, CancellationToken ct = default);

    /// <summary>
    /// Takes back the attempt in progress, for failures that are not the document's (a tool
    /// missing, the worker shutting down). The sweep tries the document again on its next cycle.
    /// </summary>
    Task AbandonAttemptAsync(Guid contentObjectId, CancellationToken ct = default);

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

    /// <summary>Live rows per status, for the statuses that occur.</summary>
    Task<IReadOnlyList<DocumentTextStatusCount>> GetStatusCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Content the sweep can read (as in <see cref="GetExtractionCandidatesAsync"/>) that has no
    /// row from <paramref name="extractorVersion"/> or newer: never read, or read by an older
    /// extractor. Failed rows are counted by status instead.
    /// </summary>
    Task<int> CountWaitingAsync(IReadOnlyCollection<string> mimeTypes, int extractorVersion,
        CancellationToken ct = default);

    /// <summary>
    /// The most recent failures, newest first. The file name, type and error message are filled in
    /// only for content one of <paramref name="viewerId"/>'s own files holds, because an admin
    /// never sees another account's library.
    /// </summary>
    Task<IReadOnlyList<DocumentTextFailure>> GetRecentFailuresAsync(Guid viewerId, int maxAttempts,
        int limit, CancellationToken ct = default);

    /// <summary>
    /// Clears the attempt count and last attempt of every failed row, so the sweep's next cycle
    /// tries them again. Returns how many rows it reset.
    /// </summary>
    Task<int> ResetFailedAsync(CancellationToken ct = default);
}