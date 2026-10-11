using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Dto.Files.Streaming.Shuffle;
using System.Security.Cryptography;
using System.Text;

namespace Alexandria.Services.Streaming.Wrapped;

public sealed class ListeningStatsService(IUnitOfWork unitOfWork) : IListeningStatsService
{
    public async Task<WrappedDeckResponse> GetWrappedAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var sessions = await unitOfWork.StreamingHistories
            .GetListeningSessionRowsAsync(userId, fromUtc, toUtc, ct);

        var histories = await unitOfWork.StreamingHistories
            .GetListeningHistoryRefsAsync(userId, ct, fromUtc);

        var raw = ListeningStatsCompute.Compute(fromUtc, toUtc, sessions, histories);
        var identity = VisualIdentity(userId, fromUtc);

        return new WrappedDeckResponse(fromUtc, toUtc, ListeningStatsCurate.Curate(raw, identity))
        {
            GeneratedAt = DateTime.UtcNow,
            VisualIdentity = identity,
            Summary = SummaryOf(raw)
        };
    }

    // The opaque identity is stable for an evolving year; SVG instance IDs are generated separately in Vue.
    private static string VisualIdentity(Guid userId, DateTime fromUtc)
    {
        var seed = $"alexandria-wrapped-v1:{userId}:{fromUtc:yyyy-MM-dd}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..16];
    }

    private static WrappedSummaryFacts SummaryOf(RawListeningStats raw)
    {
        return new WrappedSummaryFacts
        {
            Seconds = raw.TotalListenedSeconds, Sessions = raw.SessionCount,
            QualifiedPlayCount = raw.QualifiedPlayCount,
            Tracks = raw.TopSongs.Count, Artists = raw.TopArtists.Count, ActiveDays = raw.Insights.ActiveDays,
            KnownArtistShare = raw.Insights.KnownArtistShare, HasPriorHistory = raw.Insights.HasPriorHistory,
            HistoryFrom = raw.Insights.HistoryFrom
        };
    }

    public async Task<TopSongsResponse> GetTopSongsAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
    {
        var ranking = await unitOfWork.StreamingHistories
            .GetTopSongsAsync(userId, fromUtc, toUtc, limit, ct);

        if (ranking.Count == 0)
            return new TopSongsResponse(fromUtc, toUtc, []);

        var references = ranking.Select(s => new PlaybackSourceEntryRef { FileId = s.FileId }).ToList();
        var playableFiles = await unitOfWork.Files.GetPlaybackEntriesAsync(
            userId, new PlaybackSourceDto { IsVideo = false }, references, ct);
        var filesById = playableFiles.ToDictionary(f => f.FileId);

        var items = ranking
            .Where(s => filesById.ContainsKey(s.FileId))
            .Select(s => new TopSongDto(filesById[s.FileId], s.QualifiedPlayCount, s.TotalListenedSeconds))
            .ToList();

        return new TopSongsResponse(fromUtc, toUtc, items);
    }

    public async Task<TimelineResponse> GetTimelineAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var sessions = await unitOfWork.StreamingHistories
            .GetListeningSessionRowsAsync(userId, fromUtc, toUtc, ct);

        return ListeningTimeline.Build(fromUtc, toUtc, sessions);
    }
}
