using System.Net;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public sealed record PreviewSizeBackfillResult(
    int Scanned,
    int Backfilled,
    int Missing,
    int Skipped,
    int Failed);

/// <summary>
/// One-shot backfill for preview rows that predate size tracking. Reads each
/// missing size from Garage object metadata and writes it back only while no
/// size is recorded, so reruns and concurrent writers are safe.
/// </summary>
public class PreviewSizeBackfillService(
    IPreviewRepository previews,
    IAmazonS3 s3,
    IOptions<S3Config> config,
    ILogger<PreviewSizeBackfillService> logger)
{
    private const int BatchSize = 500;

    public async Task<PreviewSizeBackfillResult> BackfillAsync(CancellationToken ct = default)
    {
        var scanned = 0;
        var backfilled = 0;
        var missing = 0;
        var skipped = 0;
        var failed = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await previews.GetMissingSizesAsync(BatchSize, ct);
            if (batch.Count == 0)
                break;

            foreach (var preview in batch)
            {
                ct.ThrowIfCancellationRequested();
                scanned++;

                string key;
                try
                {
                    key = S3Service.ResolvePreviewKey(preview);
                }
                catch (InvalidOperationException ex)
                {
                    skipped++;
                    logger.LogWarning(ex, "Skipping preview {PreviewId} with no object key", preview.Id);
                    continue;
                }

                long size;
                try
                {
                    var metadata = await s3.GetObjectMetadataAsync(
                        config.Value.PreviewBucket, key, ct);
                    size = metadata.ContentLength;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    missing++;
                    logger.LogWarning(
                        "Preview {PreviewId} object {Bucket}/{Key} is missing in storage",
                        preview.Id, config.Value.PreviewBucket, key);
                    continue;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Failed to read size for preview {PreviewId}", preview.Id);
                    continue;
                }

                if (await previews.TryBackfillSizeAsync(preview.Id, size, ct))
                    backfilled++;
            }
        }

        return new PreviewSizeBackfillResult(scanned, backfilled, missing, skipped, failed);
    }
}