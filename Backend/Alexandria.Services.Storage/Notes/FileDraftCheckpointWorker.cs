using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Notes;

/// <summary>
/// Turns drafts nobody has written to for an hour into versions. An open editor checkpoints on
/// its own when the note is left, so what remains here is mostly a tab that was closed or
/// crashed; until then, search and folder policies would never see that text.
/// </summary>
public sealed partial class FileDraftCheckpointWorker(
    IServiceProvider serviceProvider,
    ILogger<FileDraftCheckpointWorker> logger)
    : BackgroundService
{
    internal static readonly TimeSpan IdleAge = TimeSpan.FromHours(1);

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSweepFailed(logger, ex);
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStopped(logger);
        }
    }

    /// <summary>Checkpoints one batch of idle drafts and returns how many became versions.</summary>
    internal async Task<int> SweepAsync(DateTime now, CancellationToken ct)
    {
        IReadOnlyList<IdleFileDraft> idle;

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            idle = await unitOfWork.FileDrafts.GetIdleAsync(now - IdleAge, BatchSize, ct);
        }

        var checkpointed = 0;

        foreach (var draft in idle)
        {
            if (await CheckpointAsync(draft, ct)) checkpointed++;
        }

        if (checkpointed > 0) LogCheckpointed(logger, checkpointed);

        return checkpointed;
    }

    // Each draft gets its own scope, so one that fails leaves nothing behind in the next one's
    // change tracker, and its text is read here so only one draft's is held at a time.
    private async Task<bool> CheckpointAsync(IdleFileDraft draft, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<AuditContext>().RunAsSystem();

        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var drafts = services.GetRequiredService<IFileDraftService>();

        try
        {
            var content = await unitOfWork.FileDrafts.GetContentAsync(draft.FileId, draft.Revision, ct);

            // Written, checkpointed or discarded since the batch was read: no longer idle.
            if (content is null)
            {
                LogDraftMovedOn(logger, draft.FileId);

                return false;
            }

            await drafts.CheckpointAsync(draft.FileId, draft.BaseVersionId, draft.Revision, content, draft.OwnerId,
                ct);

            return true;
        }
        catch (Exception ex) when (ex is FileVersionConflictException or FileDraftConflictException)
        {
            // An editor wrote the draft or saved a version after the batch was read; it is no
            // longer idle, so it is left to that editor.
            LogDraftMovedOn(logger, draft.FileId);

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCheckpointFailed(logger, ex, draft.FileId);

            return false;
        }
    }
}
