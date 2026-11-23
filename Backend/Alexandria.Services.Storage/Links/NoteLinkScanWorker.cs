using Alexandria.Common;
using Alexandria.Common.Notes;
using Alexandria.Common.Services;
using Alexandria.Dto.Links;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Links;

/// <summary>
/// Indexes the links of notes the editor did not save: uploads, restored versions nobody read yet,
/// markdown moved into Notes, and every note that existed before the index did. Saves from the
/// editor are indexed on the spot, so this is the slow path and runs at a gentle pace.
/// </summary>
public sealed partial class NoteLinkScanWorker(
    IServiceProvider serviceProvider,
    ILogger<NoteLinkScanWorker> logger)
    : BackgroundService
{
    internal const int BatchSize = 50;
    internal const int MaxFailuresInARow = 3;

    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);

    // A full batch means more are waiting, as during the first backfill.
    private static readonly TimeSpan BusyDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = IdleDelay;

                try
                {
                    var listed = await SweepAsync(stoppingToken);

                    if (listed == BatchSize) delay = BusyDelay;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSweepFailed(logger, ex);
                }

                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStopped(logger);
        }
    }

    /// <summary>Indexes one batch and returns how many notes were listed for it.</summary>
    internal async Task<int> SweepAsync(CancellationToken ct)
    {
        IReadOnlyList<NoteLinkScanCandidate> candidates;

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            candidates = await unitOfWork.NoteLinks.GetScanCandidatesAsync(NoteLinkExtractor.CurrentVersion,
                TextContentLimits.MaxBytes, BatchSize, ct);
        }

        var failuresInARow = 0;

        // Each note gets its own scope, so a failed save leaves nothing in the next one's tracker.
        foreach (var candidate in candidates)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var index = scope.ServiceProvider.GetRequiredService<INoteLinkIndexService>();

            try
            {
                await index.IndexCandidateAsync(candidate, ct);
                failuresInARow = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogNoteFailed(logger, ex, candidate.VersionId);
                failuresInARow++;

                // Several in a row point at storage rather than the notes, and the rest of the
                // batch would only fail the same way.
                if (failuresInARow == MaxFailuresInARow)
                {
                    LogBatchStopped(logger, failuresInARow);
                    break;
                }
            }
        }

        if (candidates.Count > 0) LogSwept(logger, candidates.Count);

        return candidates.Count;
    }
}
