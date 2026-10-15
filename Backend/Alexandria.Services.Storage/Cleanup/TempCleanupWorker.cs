using System.Net;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models.Enumerators;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public class TempCleanupWorker(
    IServiceProvider serviceProvider,
    IAmazonS3 s3,
    IOptions<S3Config> config,
    ILogger<TempCleanupWorker> logger)
    : BackgroundService
{
    private const string ContentPrefix = "content/";
    private const int S3PageSize = 1000;
    private const int DbBatchSize = 500;

    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _abandonedAge = TimeSpan.FromHours(72);
    private readonly TimeSpan _redundantAge = TimeSpan.FromHours(1);
    private readonly TimeSpan _interruptedAge = TimeSpan.FromHours(72);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TempCleanupWorker started");

        // Wait a bit before first run to let app fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepTempBucketAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during temp cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        logger.LogInformation("TempCleanupWorker stopped");
    }

    /// <summary>
    /// Enumerates the temp bucket but decides from the database. A temp object
    /// is deleted only when it is abandoned, redundant (its bytes are promoted),
    /// or leftover from an interrupted attempt. Temps that are the sole copy of
    /// an unpromoted upload are never touched; exhausted ones are quarantined
    /// for later manual intervention instead of being deleted.
    /// </summary>
    internal async Task SweepTempBucketAsync(CancellationToken ct)
    {
        var bucket = config.Value.TempBucket;

        if (string.IsNullOrWhiteSpace(bucket))
        {
            logger.LogWarning("Temp cleanup skipped: temp bucket is not configured");
            return;
        }

        var now = DateTime.UtcNow;
        var deletedCount = 0;
        var quarantinedCount = 0;
        var errorCount = 0;

        logger.LogInformation("Starting temp cleanup sweep");

        var listRequest = new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = ContentPrefix,
            MaxKeys = S3PageSize
        };

        try
        {
            do
            {
                var response = await s3.ListObjectsV2Async(listRequest, ct);

                if (response.S3Objects is null)
                    break;

                var tempKeys = response.S3Objects
                    .Select(o => o.Key)
                    .Where(k => k.StartsWith(ContentPrefix, StringComparison.Ordinal))
                    .Select(k => k.Substring(ContentPrefix.Length))
                    .Where(k => k.Length > 0)
                    .ToList();

                foreach (var batch in tempKeys.Chunk(DbBatchSize))
                {
                    if (ct.IsCancellationRequested)
                        break;

                    try
                    {
                        var (deleted, quarantined) = await SweepBatchAsync(bucket, batch, now, ct);
                        deletedCount += deleted;
                        quarantinedCount += quarantined;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        logger.LogWarning(ex, "Failed to sweep a temp batch of {Count} keys", batch.Length);
                    }
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            } while ((listRequest.ContinuationToken is not null) && !ct.IsCancellationRequested);

            logger.LogInformation(
                "Temp cleanup completed: {DeletedCount} deleted, {QuarantinedCount} quarantined, {ErrorCount} errors",
                deletedCount, quarantinedCount, errorCount);

            if (errorCount > 10)
            {
                await AlertCleanupFailuresAsync(errorCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list temp bucket objects");
            throw;
        }
    }

    /// <summary>
    /// Resolves one batch of temp keys against the database and deletes what is
    /// safe. Returns deleted and quarantined counts.
    /// </summary>
    internal async Task<(int Deleted, int Quarantined)> SweepBatchAsync(
        string bucket, string[] tempKeys, DateTime now, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var uploads = await unitOfWork.Uploads.GetByTempKeysAsync(tempKeys, ct);
        var uploadsByKey = uploads.ToDictionary(u => u.TempObjectKey, u => u);

        var finishedHashes = uploads
            .Where(u => u.Status == UploadStatus.Finished)
            .Select(u => u.Hash)
            .Distinct()
            .ToList();

        var contentObjects = await unitOfWork.ContentObjects.GetByHashesAsync(finishedHashes, ct);

        // One live row per hash is enforced by ix_contentobjects_not_deleted,
        // so duplicates throw here loudly instead of being silently picked.
        var contentByHash = contentObjects
            .ToDictionary(c => Convert.ToHexStringLower(c.Hash));

        var deleted = 0;
        var quarantined = 0;
        var abandonedTouched = false;

        foreach (var tempKey in tempKeys)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!uploadsByKey.TryGetValue(tempKey, out var upload))
            {
                // No upload row owns this key. Never delete what the database
                // cannot account for; it may predate upload tracking.
                logger.LogDebug("Skipping unowned temp object: {Key}", tempKey);
                continue;
            }

            var referenceTime = upload.FinishedAt ?? upload.CreatedAt;

            switch (upload.Status)
            {
                case UploadStatus.InProgress when upload.CreatedAt > now - _abandonedAge:
                    continue;
                case UploadStatus.InProgress:
                {
                    if (await DeleteTempKeyAsync(bucket, tempKey, ct))
                        deleted++;

                    upload.Status = UploadStatus.Interrupted;
                    upload.FinishedAt = now;
                    unitOfWork.Uploads.Update(upload);
                    abandonedTouched = true;

                    continue;
                }
                case UploadStatus.Finished:
                {
                    contentByHash.TryGetValue(Convert.ToHexStringLower(upload.Hash), out var contentObject);

                    if (contentObject is not null
                        && contentObject.IsPromoted
                        && referenceTime <= now - _redundantAge)
                    {
                        // Durable copy confirmed. The temp copy is redundant.
                        if (await DeleteTempKeyAsync(bucket, tempKey, ct))
                            deleted++;

                        continue;
                    }

                    var exhausted = contentObject is null
                                    || (!contentObject.IsPromoted && contentObject.PromotionAttempts >= 10);

                    if (exhausted && referenceTime <= now - _redundantAge)
                    {
                        // Sole copy or no durable copy and no retries left.
                        // Retain for manual intervention;
                        quarantined++;
                        logger.LogWarning(
                            "Quarantining temp object without durable copy: {Key} (upload {UploadId})",
                            tempKey, upload.Id);
                    }

                    continue;
                }
                case UploadStatus.Interrupted when upload.CreatedAt <= now - _interruptedAge:
                {
                    if (await DeleteTempKeyAsync(bucket, tempKey, ct))
                        deleted++;
                    break;
                }
                default:
                    logger.LogError("Temp cleanup worker could not handle the status");
                    break;
            }
        }

        if (abandonedTouched)
            await unitOfWork.SaveChangesAsync(ct);

        return (deleted, quarantined);
    }

    private async Task<bool> DeleteTempKeyAsync(string bucket, string tempKey, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(bucket, $"{ContentPrefix}{tempKey}", ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("Temp object already absent: {Key}", tempKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete temp object: {Key}", tempKey);
            return false;
        }
    }

    private Task AlertCleanupFailuresAsync(int errorCount)
    {
        // TODO: Implement alerting
        logger.LogCritical(
            "ALERT: Temp cleanup encountered {ErrorCount} errors!",
            errorCount);

        return Task.CompletedTask;
    }
}