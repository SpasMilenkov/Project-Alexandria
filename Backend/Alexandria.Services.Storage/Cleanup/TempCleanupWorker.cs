using Alexandria.Common.Config;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public class TempCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAmazonS3 _s3;
    private readonly IOptions<S3Config> _config;
    private readonly ILogger<TempCleanupWorker> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _fileAge = TimeSpan.FromHours(3);

    public TempCleanupWorker(
        IServiceProvider serviceProvider,
        IAmazonS3 s3,
        IOptions<S3Config> config,
        ILogger<TempCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _s3 = s3;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TempCleanupWorker started");

        // Wait a bit before first run to let app fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldTempFilesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("TempCleanupWorker stopped");
    }

    private async Task CleanupOldTempFilesAsync(CancellationToken ct)
    {
        var cutoffTime = DateTime.UtcNow - _fileAge;
        var deletedCount = 0;
        var errorCount = 0;

        _logger.LogInformation(
            "Starting temp cleanup for files older than {CutoffTime}",
            cutoffTime);

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _config.Value.TempBucket,
                Prefix = "content/"
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3.ListObjectsV2Async(listRequest, ct);

                if (response.S3Objects is null) break;

                foreach (var s3Object in response.S3Objects)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (s3Object.LastModified < cutoffTime)
                    {
                        try
                        {
                            await _s3.DeleteObjectAsync(
                                _config.Value.TempBucket,
                                s3Object.Key,
                                ct);

                            deletedCount++;

                            _logger.LogDebug(
                                "Deleted temp object: {Key} (age: {Age})",
                                s3Object.Key,
                                DateTime.UtcNow - s3Object.LastModified);
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            _logger.LogWarning(
                                ex,
                                "Failed to delete temp object: {Key}",
                                s3Object.Key);
                        }
                    }
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            } while ((response.IsTruncated ?? false) && !ct.IsCancellationRequested);

            _logger.LogInformation(
                "Temp cleanup completed: {DeletedCount} deleted, {ErrorCount} errors",
                deletedCount, errorCount);

            if (errorCount > 10)
            {
                await AlertCleanupFailuresAsync(errorCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list temp bucket objects");
            throw;
        }
    }

    private Task AlertCleanupFailuresAsync(int errorCount)
    {
        // TODO: Implement alerting
        _logger.LogCritical(
            "ALERT: Temp cleanup encountered {ErrorCount} errors!",
            errorCount);

        return Task.CompletedTask;
    }
}