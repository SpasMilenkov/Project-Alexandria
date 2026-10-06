using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Dto.OverviewSummaries;
using System.Security.Cryptography;
using System.Text;

namespace Alexandria.Services.Streaming.Wrapped;

public sealed class WrappedSummaryGenerator(IUnitOfWork unitOfWork) : ISummaryGenerator
{
    public SummaryKind Kind => SummaryKind.Wrapped;

    public int SchemaVersion => 3;

    public async Task<SummaryPayload> GenerateAsync(Guid userId, SummaryPeriod period, CancellationToken ct = default)
    {
        var sessions = await unitOfWork.StreamingHistories
            .GetListeningSessionRowsAsync(userId, period.Start, period.End, ct);

        var histories = await unitOfWork.StreamingHistories
            .GetListeningHistoryRefsAsync(userId, ct, period.Start);

        var raw = ListeningStatsCompute.Compute(period.Start, period.End, sessions, histories);

        var identity = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"alexandria-wrapped-v1:{userId}:{period.Start:yyyy-MM-dd}")))[..16];

        var deck = ListeningStatsCurate.Curate(raw, identity);

        return new WrappedPayload
        {
            Summary = new WrappedSummaryFacts
            {
                Seconds = raw.TotalListenedSeconds,
                Sessions = raw.SessionCount,
                QualifiedPlayCount = raw.QualifiedPlayCount,
                Tracks = raw.TopSongs.Count,
                Artists = raw.TopArtists.Count,
                ActiveDays = raw.Insights.ActiveDays,
                KnownArtistShare = raw.Insights.KnownArtistShare,
                HasPriorHistory = raw.Insights.HasPriorHistory,
                HistoryFrom = raw.Insights.HistoryFrom
            },
            Deck = deck,
            VisualIdentity = identity,
            RecipeVersion = 2,
            CatalogVersion = ListeningComparisons.CatalogVersion
        };
    }
}
