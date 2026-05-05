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

public class OrphanedCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAmazonS3 _s3;
    private readonly IOptions<S3Config> _config;
    private readonly ILogger<OrphanedCleanupWorker> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _orphanedRetention = TimeSpan.FromDays(30);
    private const int MaxDeletionAttempts = 10;

    public OrphanedCleanupWorker(
        IServiceProvider serviceProvider,
        IAmazonS3 s3,
        IOptions<S3Config> config,
        ILogger<OrphanedCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _s3 = s3;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrphanedCleanupWorker started");

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
                _logger.LogError(ex, "Error during orphaned content cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("OrphanedCleanupWorker stopped");
    }

    private async Task CleanupOrphanedContentAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffTime = DateTime.UtcNow - _orphanedRetention;
        var now = DateTime.UtcNow;

        _logger.LogInformation(
            "Scanning for orphaned content objects (orphaned before {CutoffTime})",
            cutoffTime);


        var markedCount = await unitOfWork.ContentObjects.MarkOrphaned(now, ct);
        if (markedCount > 0)
            _logger.LogInformation("Marked {Count} ContentObjects as orphaned", markedCount);

        var clearedCount = await unitOfWork.ContentObjects.ClearOrphaned(now, ct);

        if (clearedCount > 0)
            _logger.LogInformation("Cleared orphan status from {Count} ContentObjects", clearedCount);

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
            _logger.LogDebug("No orphaned content objects found for cleanup");
            return;
        }

        _logger.LogInformation(
            "Found {Count} orphaned content objects to clean up",
            contentObjects.Count());

        var deletedCount = 0;
        var failedCount = 0;

        foreach (var contentObject in contentObjects)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                await DeleteOrphanedContentObjectAsync(contentObject, unitOfWork, ct);
                deletedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(
                    ex,
                    "Failed to delete orphaned ContentObject {Id} (hash: {Hash})",
                    contentObject.Id,
                    Convert.ToHexStringLower(contentObject.Hash));

                // Track deletion attempts
                contentObject.PromotionAttempts++; // Reuse this field for deletion attempts
                contentObject.LastPromotionAttemptAt = DateTime.UtcNow;
                unitOfWork.ContentObjects.Update(contentObject);
                await unitOfWork.CommitAsync(ct);

                // Alert if too many failures
                if (contentObject.PromotionAttempts >= MaxDeletionAttempts)
                {
                    await AlertOrphanedDeletionFailureAsync(
                        contentObject.Id,
                        contentObject.PromotionAttempts);
                }
            }
        }

        _logger.LogInformation(
            "Orphaned cleanup completed: {DeletedCount} deleted, {FailedCount} failed",
            deletedCount, failedCount);

        if (failedCount > 5)
        {
            await AlertMultipleDeletionFailuresAsync(failedCount);
        }
    }

    private async Task DeleteOrphanedContentObjectAsync(
        ContentObject contentObject,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var hash = Convert.ToHexStringLower(contentObject.Hash);
        var s3Key = $"content/{hash}";

        _logger.LogInformation(
            "Deleting orphaned ContentObject {Id} from S3: {Key}",
            contentObject.Id, s3Key);

        try
        {
            // Delete from S3
            await _s3.DeleteObjectAsync(
                _config.Value.UploadBucket,
                s3Key,
                ct);

            _logger.LogInformation(
                "Successfully deleted S3 object: {Key}",
                s3Key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex,
                "S3 object not found (already deleted?): {Key}",
                s3Key);
            // Continue with database deletion
        }

        // Hard delete from database
        await unitOfWork.ContentObjects.DeleteAsync(contentObject.Id, ct);
        await unitOfWork.CommitAsync(ct);

        _logger.LogInformation(
            "Deleted orphaned ContentObject {Id} (hash: {Hash}, orphaned since: {OrphanedAt})",
            contentObject.Id,
            hash,
            contentObject.OrphanedAt);
    }

    private Task AlertOrphanedDeletionFailureAsync(Guid contentObjectId, int attempts)
    {
        // TODO: Implement alerting
        _logger.LogCritical(
            "ALERT: ContentObject {ContentObjectId} failed orphaned deletion after {Attempts} attempts!",
            contentObjectId, attempts);

        return Task.CompletedTask;
    }

    private Task AlertMultipleDeletionFailuresAsync(int failedCount)
    {
        // TODO: Implement alerting
        _logger.LogCritical(
            "ALERT: Orphaned cleanup encountered {FailedCount} deletion failures!",
            failedCount);

        return Task.CompletedTask;
    }
}