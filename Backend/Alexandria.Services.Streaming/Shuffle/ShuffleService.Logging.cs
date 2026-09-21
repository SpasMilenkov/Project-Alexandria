using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Shuffle;

public sealed partial class ShuffleService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Created shuffle session with {CandidateCount} candidates in {QueryMs}ms query, {RankMs}ms rank (video: {IsVideo}, playlist: {IsPlaylist})")]
    private partial void LogSessionCreated(
        int candidateCount, long queryMs, long rankMs, bool isVideo, bool isPlaylist);
}