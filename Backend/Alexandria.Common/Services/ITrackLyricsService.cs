using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;

namespace Alexandria.Common.Services;

public interface ITrackLyricsService
{
    /// <summary>
    /// Retrieves the lyrics for a given transpilation job, if they exist.
    /// </summary>
    /// <param name="jobId">The ID of the transpilation job to retrieve lyrics for.</param>
    /// <param name="userId">The ID of the requesting user, used to verify ownership of the job.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lyrics associated with the job.</returns>
    /// <exception cref="LyricsNotFoundException">Thrown when no lyrics exist for the given job.</exception>
    Task<LyricsDto> GetLyricsForMediaAsync(Guid jobId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Queues a lyrics fetch for the given transpilation job.
    /// If a lyrics entity already exists and is not user-submitted, it is reset to pending and re-queued.
    /// If no lyrics entity exists, one is created before publishing to the worker.
    /// </summary>
    /// <param name="jobId">The ID of the transpilation job to fetch lyrics for.</param>
    /// <param name="userId">The ID of the requesting user, used to verify ownership of the job.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the existing lyrics were submitted manually by the user.</exception>
    Task QueueLyricsRefetchAsync(Guid jobId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Soft deletes the lyrics entity with the given ID.
    /// </summary>
    /// <param name="lyricsId">The ID of the lyrics entity to remove.</param>
    /// <param name="userId">The ID of the requesting user, used to verify ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="LyricsNotFoundException">Thrown when no lyrics exist with the given ID for this user.</exception>
    Task RemoveLyricsAsync(Guid lyricsId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Uploads user-provided lyrics for the given transpilation job.
    /// If a lyrics entity already exists, it is overwritten regardless of its current provider.
    /// User-submitted lyrics are protected from automatic overwriting by the lyrics worker.
    /// </summary>
    /// <param name="jobId">The ID of the transpilation job to attach lyrics to.</param>
    /// <param name="userId">The ID of the requesting user, used to verify ownership of the job.</param>
    /// <param name="plainLyrics">Plain text lyrics without timestamps. At least one of plain or synced must be provided.</param>
    /// <param name="syncedLyrics">LRC-formatted synced lyrics with per-line timestamps. At least one of plain or synced must be provided.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when both plain and synced lyrics are null.</exception>
    Task UploadLyricsAsync(Guid jobId, Guid userId, string? plainLyrics, string? syncedLyrics,
        CancellationToken ct = default);

    /// <summary>
    /// Changes the lyrics provider for an existing lyrics entity and re-queues a fetch using the new provider.
    /// Clears all previously fetched data before publishing to the worker.
    /// Cannot be used on user-submitted lyrics; call <see cref="RemoveLyricsAsync"/> first.
    /// </summary>
    /// <param name="lyricsId">The ID of the lyrics entity to update.</param>
    /// <param name="userId">The ID of the requesting user, used to verify ownership.</param>
    /// <param name="provider">The new provider to use for fetching lyrics. Cannot be <see cref="LyricsProvider.Manual"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="provider"/> is <see cref="LyricsProvider.Manual"/>.</exception>
    /// <exception cref="LyricsNotFoundException">Thrown when no lyrics exist with the given ID for this user.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the lyrics were submitted manually by the user.</exception>
    Task ChangeProviderAsync(
        Guid lyricsId,
        Guid userId,
        LyricsProvider provider,
        CancellationToken ct = default);
}