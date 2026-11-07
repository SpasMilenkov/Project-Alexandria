using System.Diagnostics;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Content;

public sealed partial class UploadBatchService(
    IServiceScopeFactory scopeFactory,
    ILogger<UploadBatchService> logger)
    : IUploadBatchService
{
    private const int VerifyParallelism = 4;
    private const int CommitParallelism = 4;

    // One retry covers a unique-index collision with another batch finalizing identical
    // bytes: the second attempt finds that content object and reuses it.
    private const int CommitAttempts = 2;

    public async Task<IReadOnlyList<UploadFinalizeResult>> FinalizeAsync(
        IReadOnlyList<UploadFinalizeItem> items,
        Guid userId,
        CancellationToken ct = default)
    {
        var results = new UploadFinalizeResult?[items.Count];
        var verified = new VerifiedUpload?[items.Count];

        var finishedIds = await GetFinishedIdsAsync(items, userId, ct);
        var pending = new List<int>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (finishedIds.Contains(items[i].UploadId))
                results[i] = new UploadFinalizeResult(items[i].UploadId, true, null);
            else
                pending.Add(i);
        }

        var verifyTimer = Stopwatch.StartNew();
        var commitElapsed = TimeSpan.Zero;

        try
        {
            await VerifyAllAsync(items, pending, userId, results, verified, ct);
            verifyTimer.Stop();

            var commitTimer = Stopwatch.StartNew();

            await CommitAllAsync(items, pending, results, verified, ct);

            commitElapsed = commitTimer.Elapsed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await AbandonUncommittedAsync(results, verified);
            throw;
        }

        var failed = results.Count(result => result is { Success: false });
        LogBatchFinalized(logger, items.Count, failed, verifyTimer.Elapsed, commitElapsed);

        return results.Select(result => result!).ToList();
    }

    private async Task<HashSet<Guid>> GetFinishedIdsAsync(
        IReadOnlyList<UploadFinalizeItem> items,
        Guid userId,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.Uploads.GetFinishedIdsAsync(
            items.Select(item => item.UploadId).ToList(),
            userId,
            ct);
    }

    private Task VerifyAllAsync(
        IReadOnlyList<UploadFinalizeItem> items,
        List<int> pending,
        Guid userId,
        UploadFinalizeResult?[] results,
        VerifiedUpload?[] verified,
        CancellationToken ct)
    {
        var options = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = VerifyParallelism
        };

        return Parallel.ForEachAsync(pending, options, async (i, token) =>
        {
            var item = items[i];

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();

                verified[i] = await storage.VerifyUploadAsync(item.UploadId, userId, item.DirectoryId, token);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                LogItemFailed(logger, ex, item.UploadId);
                results[i] = new UploadFinalizeResult(item.UploadId, false, ToClientError(ex));
            }
        });
    }

    private async Task CommitAllAsync(
        IReadOnlyList<UploadFinalizeItem> items,
        List<int> pending,
        UploadFinalizeResult?[] results,
        VerifiedUpload?[] verified,
        CancellationToken ct)
    {
        var toCommit = pending.Where(i => verified[i] is not null).ToList();
        var (independent, conflicting) = SplitByConflicts(items, verified, toCommit);

        var options = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = CommitParallelism
        };

        await Parallel.ForEachAsync(independent, options, async (i, token) =>
        {
            results[i] = await CommitAsync(items[i], verified[i]!, token);
        });

        foreach (var i in conflicting)
        {
            ct.ThrowIfCancellationRequested();

            results[i] = await CommitAsync(items[i], verified[i]!, ct);
        }
    }

    // Commits are check-then-insert, so two items race if they share content (the unique hash
    // index) or a destination (both would create the file, or both would claim one version
    // number). Those go one at a time, and the rest can't affect each other.
    private static (List<int> Independent, List<int> Conflicting) SplitByConflicts(
        IReadOnlyList<UploadFinalizeItem> items,
        VerifiedUpload?[] verified,
        List<int> indexes)
    {
        var contentCounts = indexes
            .GroupBy(i => Convert.ToHexString(verified[i]!.Hash))
            .ToDictionary(group => group.Key, group => group.Count());

        var destinationCounts = indexes
            .GroupBy(i => (items[i].DirectoryId, items[i].FileName))
            .ToDictionary(group => group.Key, group => group.Count());

        var independent = new List<int>(indexes.Count);
        var conflicting = new List<int>();

        foreach (var i in indexes)
        {
            var isUnique = contentCounts[Convert.ToHexString(verified[i]!.Hash)] == 1
                           && destinationCounts[(items[i].DirectoryId, items[i].FileName)] == 1;

            if (isUnique)
                independent.Add(i);
            else
                conflicting.Add(i);
        }

        return (independent, conflicting);
    }

    private async Task<UploadFinalizeResult> CommitAsync(
        UploadFinalizeItem item,
        VerifiedUpload upload,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            // A fresh scope per attempt: rolling back doesn't clear the change tracker, so a
            // shared one would save a failed item's entities during the next item's commit.
            await using var scope = scopeFactory.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();

            try
            {
                var committed = await storage.CommitUploadAsync(upload, item, ct);
                await DispatchAsync(scope.ServiceProvider, item, upload, committed, ct);

                return new UploadFinalizeResult(item.UploadId, true, null);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                LogItemFailed(logger, ex, item.UploadId);

                if (attempt >= CommitAttempts)
                {
                    await storage.AbandonUploadAsync(upload.UploadId, upload.TempObjectKey, CancellationToken.None);

                    return new UploadFinalizeResult(item.UploadId, false, ToClientError(ex));
                }
            }
        }
    }

    // The file is already saved at this point, so a dispatch problem must not report it as failed.
    private async Task DispatchAsync(
        IServiceProvider services,
        UploadFinalizeItem item,
        VerifiedUpload upload,
        UploadResult committed,
        CancellationToken ct)
    {
        if (item.IsEncrypted)
            return;

        try
        {
            var dispatcher = services.GetRequiredService<IPolicyDispatcher>();

            await dispatcher.DispatchAsync(new FileFinalizedEvent(
                committed.FileId,
                committed.VersionId,
                item.DirectoryId,
                committed.MimeType,
                item.FileName,
                upload.UploadedBy,
                committed.IsNewVersion), ct);
        }
        catch (Exception ex)
        {
            LogDispatchFailed(logger, ex, committed.FileId);
        }
    }

    private async Task AbandonUncommittedAsync(UploadFinalizeResult?[] results, VerifiedUpload?[] verified)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IContentStorageService>();

        for (var i = 0; i < verified.Length; i++)
        {
            if (verified[i] is not { } upload || results[i] is not null)
                continue;

            await storage.AbandonUploadAsync(upload.UploadId, upload.TempObjectKey, CancellationToken.None);
        }
    }

    private static string ToClientError(Exception ex) => ex switch
    {
        UploadVerificationException => UploadErrorMessages.FailedVerification,
        DirectoryNotFoundException => UploadErrorMessages.DestinationNotFound,
        _ => UploadErrorMessages.Generic
    };
}
