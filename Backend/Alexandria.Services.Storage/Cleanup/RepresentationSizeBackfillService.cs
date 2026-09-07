using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public sealed record RepresentationSizeBackfillResult(
    int Scanned,
    int Backfilled,
    int Jobs,
    int Unmatched,
    int Failed);

/// <summary>
/// One-shot backfill for representation rows that predate size tracking.
/// Representations share one segment set per job, so each job lists its prefix
/// once and lane bytes are attributed by segment file name. Lanes are emitted
/// in rung order with strictly increasing bitrates, so ordering a job's rows by
/// bitrate reconstructs the lane index without a schema change. Shared files
/// such as the manifest are negligible and intentionally uncounted.
/// </summary>
public class RepresentationSizeBackfillService(
    IStreamingRepresentationRepository representations,
    IAmazonS3 s3,
    IOptions<S3Config> config,
    ILogger<RepresentationSizeBackfillService> logger)
{
    private const int BatchSize = 200;

    public async Task<RepresentationSizeBackfillResult> BackfillAsync(CancellationToken ct = default)
    {
        var bucket = config.Value.StreamingBucket;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            logger.LogError("Streaming bucket is not configured, skipping representation size backfill");
            return new RepresentationSizeBackfillResult(0, 0, 0, 0, 0);
        }

        var scanned = 0;
        var backfilled = 0;
        var jobs = 0;
        var unmatched = 0;
        var failed = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await representations.GetMissingSizesAsync(BatchSize, ct);
            if (batch.Count == 0)
                break;

            foreach (var group in batch.GroupBy(r => r.TranspilationId))
            {
                ct.ThrowIfCancellationRequested();

                var prefix = group.First().Job.SegmentPrefix;
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    unmatched += group.Count();
                    continue;
                }

                Dictionary<int, long> laneSizes;
                try
                {
                    laneSizes = await SumLaneSizesAsync(bucket, prefix + "/", ct);
                    jobs++;
                }
                catch (Exception ex)
                {
                    failed += group.Count();
                    logger.LogError(ex, "Failed to list segments for job {JobId}", group.Key);
                    continue;
                }

                var ordered = group
                    .OrderBy(r => r.BitrateKbps)
                    .ThenBy(r => r.Width)
                    .ThenBy(r => r.Height)
                    .ThenBy(r => r.Id)
                    .ToList();

                for (var index = 0; index < ordered.Count; index++)
                {
                    scanned++;
                    if (!laneSizes.TryGetValue(index, out var size) || size <= 0)
                    {
                        unmatched++;
                        continue;
                    }

                    if (await representations.TryBackfillSizeAsync(ordered[index].Id, size, ct))
                        backfilled++;
                }
            }
        }

        return new RepresentationSizeBackfillResult(scanned, backfilled, jobs, unmatched, failed);
    }

    private async Task<Dictionary<int, long>> SumLaneSizesAsync(
        string bucket, string prefix, CancellationToken ct)
    {
        var sums = new Dictionary<int, long>();
        string? token = null;

        do
        {
            var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix,
                ContinuationToken = token,
                MaxKeys = 1000,
            }, ct);

            foreach (var obj in response.S3Objects ?? [])
            {
                if (DashSegmentClassifier.TryGetLaneIndex(obj.Key, out var lane))
                    sums[lane] = sums.GetValueOrDefault(lane) + (obj.Size ?? 0L);
            }

            token = (response.IsTruncated ?? false) ? response.NextContinuationToken : null;
        } while (token != null);

        return sums;
    }
}