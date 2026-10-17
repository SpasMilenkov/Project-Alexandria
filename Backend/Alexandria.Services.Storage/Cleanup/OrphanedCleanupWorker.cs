using System.Net;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public partial class OrphanedCleanupWorker(
    IServiceProvider serviceProvider,
    IAmazonS3 s3,
    IOptions<S3Config> config,
    ILogger<OrphanedCleanupWorker> logger)
    : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _orphanedRetention = TimeSpan.FromDays(30);
    private const int MaxDeletionAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrphanedCleanupWorker started");

        // Wait before first run
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanedContentAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during orphaned content cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        logger.LogInformation("OrphanedCleanupWorker stopped");
    }

    internal async Task CleanupOrphanedContentAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffTime = DateTime.UtcNow - _orphanedRetention;
        var now = DateTime.UtcNow;

        LogScanningForOrphanedContentObjectsOrphanedBeforeCutoffTime(cutoffTime);


        var markedCount = await unitOfWork.ContentObjects.MarkOrphaned(now, ct);
        if (markedCount > 0)
            LogMarkedCountContentObjectsAsOrphaned(markedCount);

        var clearedCount = await unitOfWork.ContentObjects.ClearOrphaned(now, ct);

        if (clearedCount > 0)
            LogClearedOrphanStatusFromCountContentObjects(clearedCount);

        // Find orphaned ContentObjects ready for deletion
        var orphanedObjects = await unitOfWork.ContentObjects
            .FindAsync(co =>
                    co.OrphanedAt != null &&
                    co.OrphanedAt < cutoffTime &&
                    co.IsPromoted && // Only delete promoted objects
                    co.DeletedAt == null, // Not already deleted
                ct);


        var contentObjects = orphanedObjects as ContentObject[] ?? orphanedObjects.ToArray();
        if (contentObjects.Length == 0)
        {
            logger.LogDebug("No orphaned content objects found for cleanup");
            return;
        }

        LogFoundCountOrphanedContentObjectsToCleanUp(contentObjects.Count());

        var deletedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var contentObject in contentObjects)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                if (await DeleteOrphanedContentObjectAsync(contentObject, unitOfWork, ct))
                    deletedCount++;
                else
                    skippedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                logger.LogError(
                    ex,
                    "Failed to delete orphaned ContentObject {Id} (hash: {Hash})",
                    contentObject.Id,
                    Convert.ToHexStringLower(contentObject.Hash));

                // Track deletion attempts. SaveChanges is used directly: this
                // scope runs without an explicit transaction, where CommitAsync
                // would be a no-op and the counter would never persist.
                contentObject.PromotionAttempts++; // Reuse this field for deletion attempts
                contentObject.LastPromotionAttemptAt = DateTime.UtcNow;
                unitOfWork.ContentObjects.Update(contentObject);
                await unitOfWork.SaveChangesAsync(ct);

                // Alert if too many failures
                if (contentObject.PromotionAttempts >= MaxDeletionAttempts)
                {
                    await AlertOrphanedDeletionFailureAsync(
                        contentObject.Id,
                        contentObject.PromotionAttempts);
                }
            }
        }

        LogOrphanedCleanupCompleted(deletedCount, skippedCount, failedCount);

        if (failedCount > 5)
        {
            await AlertMultipleDeletionFailuresAsync(failedCount);
        }
    }

    internal async Task<bool> DeleteOrphanedContentObjectAsync(
        ContentObject contentObject,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var hash = Convert.ToHexStringLower(contentObject.Hash);
        var s3Key = $"content/{hash}";

        // Atomic claim: hard-deletes the row only if it is still orphaned,
        // past retention, and referenced by no live version. A concurrent
        // revive either commits first (the claim sees the new version and
        // skips) or commits after (its version insert fails loudly on the
        // missing row instead of silently losing bytes).
        var claimed = await unitOfWork.ContentObjects.TryClaimOrphanedDeleteAsync(
            contentObject.Id, DateTime.UtcNow - _orphanedRetention, ct);

        if (!claimed)
        {
            LogOrphanedDeleteSkipped(contentObject.Id);
            return false;
        }

        LogDeletingOrphanedContentObjectIdFromS3Key(contentObject.Id, s3Key);

        try
        {
            await s3.DeleteObjectAsync(
                config.Value.UploadBucket,
                s3Key,
                ct);

            LogSuccessfullyDeletedS3ObjectKey(s3Key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning(ex,
                "S3 object not found (already deleted?): {Key}",
                s3Key);
        }

        LogDeletedOrphanedContentObjectIdHashHashOrphanedSinceOrphanedAt(contentObject.Id, hash,
            contentObject.OrphanedAt);

        return true;
    }

    private Task AlertOrphanedDeletionFailureAsync(Guid contentObjectId, int attempts)
    {
        // TODO: Implement alerting
        logger.LogCritical(
            "ALERT: ContentObject {ContentObjectId} failed orphaned deletion after {Attempts} attempts!",
            contentObjectId, attempts);

        return Task.CompletedTask;
    }

    private Task AlertMultipleDeletionFailuresAsync(int failedCount)
    {
        // TODO: Implement alerting
        logger.LogCritical(
            "ALERT: Orphaned cleanup encountered {FailedCount} deletion failures!",
            failedCount);

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Marked {Count} ContentObjects as orphaned")]
    partial void LogMarkedCountContentObjectsAsOrphaned(int count);

    [LoggerMessage(LogLevel.Information, "Cleared orphan status from {Count} ContentObjects")]
    partial void LogClearedOrphanStatusFromCountContentObjects(int count);

    [LoggerMessage(LogLevel.Information, "Scanning for orphaned content objects (orphaned before {CutoffTime})")]
    partial void LogScanningForOrphanedContentObjectsOrphanedBeforeCutoffTime(DateTime cutoffTime);

    [LoggerMessage(LogLevel.Information,
        "Deleted orphaned ContentObject {Id} (hash: {Hash}, orphaned since: {OrphanedAt})")]
    partial void LogDeletedOrphanedContentObjectIdHashHashOrphanedSinceOrphanedAt(Guid id, string hash,
        DateTime? orphanedAt);

    [LoggerMessage(LogLevel.Information, "Successfully deleted S3 object: {Key}")]
    partial void LogSuccessfullyDeletedS3ObjectKey(string key);

    [LoggerMessage(LogLevel.Information, "Deleting orphaned ContentObject {Id} from S3: {Key}")]
    partial void LogDeletingOrphanedContentObjectIdFromS3Key(Guid id, string key);

    [LoggerMessage(LogLevel.Information,
        "Orphaned cleanup completed: {DeletedCount} deleted, {SkippedCount} skipped, {FailedCount} failed")]
    partial void LogOrphanedCleanupCompleted(int deletedCount, int skippedCount, int failedCount);

    [LoggerMessage(LogLevel.Debug, "Orphaned delete skipped, row revived or re-referenced: {ContentObjectId}")]
    partial void LogOrphanedDeleteSkipped(Guid contentObjectId);

    [LoggerMessage(LogLevel.Information, "Found {Count} orphaned content objects to clean up")]
    partial void LogFoundCountOrphanedContentObjectsToCleanUp(int count);
}