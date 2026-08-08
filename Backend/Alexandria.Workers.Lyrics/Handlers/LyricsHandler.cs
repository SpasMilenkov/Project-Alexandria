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
        var lyricsRecord = await unitOfWork.Lyrics.FirstOrDefaultAsync(
                               l => l.Id == message && l.DeletedAt == null, ct)
                           ?? throw new LyricsNotFoundException(message);

        var searchParams = await unitOfWork.Lyrics.GetSearchParamsAsync(message, ct)
                           ?? throw new LyricsNotFoundException(message);

        // If a provider was explicitly chosen by the user, respect it.
        // If this is a general refetch (PendingFetch with no prior successful provider),
        // let the composite try all.
        var targetProvider = lyricsRecord.SourceProvider != LyricsProvider.None
            ? lyricsRecord.SourceProvider
            : (LyricsProvider?)null;
        try
        {
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
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            LogFetchFailedWithErrorEx(logger, ex);
            lyricsRecord.Status = LyricsStatus.FetchFailed;
            unitOfWork.Lyrics.Update(lyricsRecord);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    [LoggerMessage(23000, LogLevel.Error, "Fetch failed with error")]
    static partial void LogFetchFailedWithErrorEx(ILogger logger, Exception ex);
}