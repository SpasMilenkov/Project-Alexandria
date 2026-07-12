using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Lyrics;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Lyrics;

public sealed partial class CompositeLyricsProvider(
    IEnumerable<ITrackLyricsProvider> providers,
    ILogger<CompositeLyricsProvider> logger)
{
    private static readonly TimeSpan PerProviderTimeout = TimeSpan.FromSeconds(40);

    private readonly IReadOnlyList<ITrackLyricsProvider> _ordered =
        providers.OrderByDescending(p => p.Priority).ToList();

    public async Task<LyricsResult?> FetchAsync(
        string trackName,
        string? artistName,
        string? albumName,
        double? durationSeconds,
        LyricsProvider? targetProvider = null,
        CancellationToken ct = default)
    {
        var candidates = targetProvider.HasValue
            ? _ordered.Where(p => p.Provider == targetProvider.Value).ToList()
            : _ordered;

        if (targetProvider.HasValue && candidates.Count == 0)
        {
            logger.LogWarning("Requested provider {Provider} is not registered, falling back to full chain",
                targetProvider.Value);
            candidates = _ordered;
        }

        foreach (var provider in candidates)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(PerProviderTimeout);

            try
            {
                if (!await provider.IsAvailableAsync(timeoutCts.Token))
                {
                    LogProviderUnavailableSkipping(provider.GetType().Name);
                    continue;
                }

                var result = await provider.FetchAsync(
                    trackName, artistName, albumName, durationSeconds, timeoutCts.Token);

                if (result is not null)
                {
                    logger.LogInformation("Lyrics found via {Provider}", provider.GetType().Name);
                    return result;
                }

                logger.LogDebug("{Provider} found nothing, trying next", provider.GetType().Name);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // timeoutCts fired, not the caller's token -> treat as this provider's failure
                LogProviderTimedOutAfterTimeoutSTryingNext(provider.GetType().Name, PerProviderTimeout.TotalSeconds);
            }
            catch (Exception ex)
            {
                // Defensive: a well-behaved provider shouldn't throw (see note below),
                // but don't let one bad provider kill the whole chain.
                logger.LogError(ex, "{Provider} threw unexpectedly, trying next", provider.GetType().Name);
            }
        }

        return null;
    }

    [LoggerMessage(LogLevel.Debug, "{Provider} unavailable, skipping")]
    partial void LogProviderUnavailableSkipping(string provider);

    [LoggerMessage(LogLevel.Warning, "{Provider} timed out after {Timeout}s, trying next")]
    partial void LogProviderTimedOutAfterTimeoutSTryingNext(string provider, double timeout);
}