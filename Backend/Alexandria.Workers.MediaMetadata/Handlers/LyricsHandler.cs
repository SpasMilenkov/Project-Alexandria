using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming.Lyrics;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Streaming.Lyrics;

namespace AlexandriaW.Workers.MediaMetadata.Handlers;

public class LyricsHandler(
    CompositeLyricsProvider lyricsProvider,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid message, CancellationToken ct = default)
    {
        var lyricsRecord = await unitOfWork.Lyrics.FirstOrDefaultAsync(l => l.Id == message && l.DeletedAt == null, ct) ?? throw new LyricsNotFoundException(message);
        
        var searchParams = await unitOfWork.Lyrics.GetSearchParamsAsync(message, ct) ?? throw new LyricsNotFoundException(message);
        
        var result = await lyricsProvider.FetchAsync(
            searchParams.Name,
            searchParams.Artist,
            searchParams.AlbumName,
            searchParams.Duration,
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
        lyricsRecord.ConfidenceScore = 9;
        unitOfWork.Lyrics.Update(lyricsRecord);
        await unitOfWork.SaveChangesAsync(ct);
    }
}