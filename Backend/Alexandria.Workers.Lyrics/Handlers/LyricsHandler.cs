using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming.Lyrics;

namespace Alexandria.Workers.Lyrics.Handlers;

public partial class LyricsHandler(
    ILogger<LyricsHandler> logger,
    CompositeLyricsProvider lyricsProvider,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid message, CancellationToken ct = default)
    {
        var jobId = message;
        var lyricsRecord = await unitOfWork.Lyrics.GetByJobIdAsync(jobId, ct)
                           ?? throw new LyricsNotFoundException(jobId);

        // Lost the claim race: another delivery is handling (or finished) this
        // job. Return silently so the worker acks a superseded delivery.
        if (!await unitOfWork.Jobs.TryClaimJobAsync(jobId, ct))
        {
            LogDuplicateDelivery(logger, jobId);
            return;
        }

        try
        {
            var searchParams = await unitOfWork.Lyrics.GetSearchParamsAsync(lyricsRecord.Id, ct)
                               ?? throw new LyricsNotFoundException(jobId);

            // If a provider was explicitly chosen by the user, respect it.
            // If this is a general refetch (PendingFetch with no prior successful provider),
            // let the composite try all.
            var targetProvider = lyricsRecord.SourceProvider != LyricsProvider.None
                ? lyricsRecord.SourceProvider
                : (LyricsProvider?)null;
            var result = await lyricsProvider.FetchAsync(
                searchParams.Name,
                searchParams.Artist,
                searchParams.AlbumName,
                searchParams.Duration,
                targetProvider,
                ct);
            if (result == null)
            {
                lyricsRecord.Status = LyricsStatus.NoMatch;
                lyricsRecord.FetchedAt = DateTime.UtcNow;
                unitOfWork.Lyrics.Update(lyricsRecord);
                await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Ready, ct: ct);
                await unitOfWork.SaveChangesAsync(ct);
                return;
            }

            lyricsRecord.PlainLyrics = result.PlainLyrics;
            lyricsRecord.SyncedLyrics = result.SyncedLyrics;
            lyricsRecord.ProviderTrackId = result.ProviderTrackId;
            lyricsRecord.SourceProvider = result.Provider;
            lyricsRecord.IsInstrumental = result.Instrumental;
            lyricsRecord.FetchedAt = DateTime.UtcNow;
            lyricsRecord.Cached = true;
            lyricsRecord.Status = LyricsStatus.Fetched;
            //Lrclib doesn't have a concept of confidence, since we are matching with duration it is arbitrarily set high
            //TODO: For future me, I have to figure out what to do with the values from different providers
            lyricsRecord.ConfidenceScore = result.ConfidenceScore;
            unitOfWork.Lyrics.Update(lyricsRecord);
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Ready, progress: 100, ct: ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (LyricsNotFoundException)
        {
            // Inconsistent but terminal: record the outcome, then rethrow so the
            // worker rejects (drops) instead of acking silently.
            await FailJobAsync(jobId, "search parameters not found", ct);
            throw;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && !IsTransient(ex))
        {
            LogFetchFailedWithErrorEx(logger, ex);
            lyricsRecord.Status = LyricsStatus.FetchFailed;
            unitOfWork.Lyrics.Update(lyricsRecord);
            await FailJobAsync(jobId, ex.Message, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    // Network-level failures are worth another delivery: the job stays
    // Processing and the worker's requeue keeps it alive. Cancellation and
    // anything else propagate to the terminal handling above.
    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException;

    private async Task FailJobAsync(Guid jobId, string errorDetail, CancellationToken ct)
        => await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: errorDetail, ct: ct);

    [LoggerMessage(23000, LogLevel.Error, "Fetch failed with error")]
    static partial void LogFetchFailedWithErrorEx(ILogger logger, Exception ex);

    [LoggerMessage(23001, LogLevel.Information, "Lyrics job {JobId} already claimed; skipping duplicate delivery")]
    static partial void LogDuplicateDelivery(ILogger logger, Guid jobId);
}