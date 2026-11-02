using Alexandria.Common;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Services;
using Microsoft.Extensions.Options;
using Directory = System.IO.Directory;

namespace Alexandria.Workers.MediaMetadata.Workers;

/// <summary>
/// Removes staged inputs orphaned by a crash or forced shutdown before any other worker starts.
/// Every staging attempt gets a unique path, so a redelivered trigger never overwrites what an
/// earlier attempt left behind; without this sweep those copies would accumulate on the volume.
/// </summary>
/// <remarks>
/// Runs in <see cref="StartAsync"/>, which the host awaits before starting the hosted services
/// registered after it, so nothing is staging or batching while it runs. Assumes this worker is
/// the only writer to <see cref="EssentiaConfig.StagedDir"/>.
/// <list type="bullet">
/// <item><c>incoming/</c> is cleared entirely: a trigger is acked only after its batch persists, so
/// every incoming file from a previous run belongs to a trigger the broker will redeliver.</item>
/// <item>Loose files directly under the staged directory are pre-batch-folder leftovers that
/// nothing reads any more.</item>
/// <item>Batch folders are kept only while their batch is still <c>Dispatched</c>; the classifier
/// may be reading those, and the results consumer or timeout sweep removes them later.</item>
/// </list>
/// </remarks>
public partial class StagingRecoveryService(
    ILogger<StagingRecoveryService> logger,
    IOptions<EssentiaConfig> essentiaOptions,
    IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RecoverAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Leftover files only cost disk space; they must never keep the worker from starting.
            LogRecoveryFailed(logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task RecoverAsync(CancellationToken ct)
    {
        var stagedDir = essentiaOptions.Value.StagedDir;
        if (!Directory.Exists(stagedDir))
            return;

        var incomingDir = StagingPaths.IncomingDirectory(stagedDir);
        var files = Directory.Exists(incomingDir) ? DeleteFiles(incomingDir) : 0;
        files += DeleteFiles(stagedDir);

        var batchDirs = Directory.EnumerateDirectories(stagedDir)
            .Select(path => (Path: path, IsBatch: Guid.TryParse(Path.GetFileName(path), out var id), Id: id))
            .Where(d => d.IsBatch)
            .ToList();

        var folders = 0;
        if (batchDirs.Count > 0)
        {
            HashSet<Guid> dispatched;
            try
            {
                using var scope = serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Nothing dispatches until this returns, so "before now" covers every dispatched batch.
                dispatched = [.. (await unitOfWork.EssentiaBatches.GetDispatchedSinceAsync(DateTime.UtcNow, ct)).Select(b => b.Id)];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Without the batch states a folder may still be in use by the classifier; keep them all.
                LogBatchLookupFailed(logger, ex, batchDirs.Count);
                LogRecovered(logger, files, 0);
                return;
            }

            foreach (var (path, _, id) in batchDirs)
            {
                if (dispatched.Contains(id))
                    continue;

                if (StagingPaths.TryDeleteDirectory(path, out var error))
                    folders++;
                else
                    LogCleanupError(logger, error, path);
            }
        }

        LogRecovered(logger, files, folders);
    }

    private int DeleteFiles(string directory)
    {
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (StagingPaths.TryDeleteFile(path, out var error))
                deleted++;
            else
                LogCleanupError(logger, error, path);
        }

        return deleted;
    }
}
