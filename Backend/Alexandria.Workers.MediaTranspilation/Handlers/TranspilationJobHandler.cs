using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Workers.MediaTranspilation.Config;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.MediaTranspilation.Handlers;

public partial class TranspilationJobHandler(
    ITranspilationJobService jobService,
    IStreamingRepresentationService representationService,
    IStorageService storage,
    IVideoTranspilationService videoTranspilation,
    IAudioTranspilationService audioTranspilation,
    IOptions<TranspilationConfig> config,
    ILogger<TranspilationJobHandler> logger)
{
    /// <summary>
    /// Attempts to claim and fully process the transpilation job identified by
    /// <paramref name="jobId"/>. If another worker already claimed the job this
    /// call is a no-op.
    /// </summary>
    public async Task HandleAsync(Guid jobId, CancellationToken ct = default)
    {
        var claimed = await jobService.TryClaimJobAsync(jobId, ct);
        if (!claimed)
            return;

        var job = await jobService.GetByIdAsync(jobId, ct);

        var mediaDir = job.IsVideo ? "v" : "a";
        var codecStr = job.IsVideo ? "h264" : "opus";
        var jobDir = Path.Combine(Path.GetTempPath(), jobId.ToString());
        var repDir = Path.Combine(jobDir, mediaDir, codecStr);
        var inputPath = Path.Combine(jobDir, "source");

        Guid? representationId = null;

        try
        {
            Directory.CreateDirectory(repDir);

            LogDownloadingSource(logger, jobId, job.ContentObjectId);

            await storage.DownloadContentObjectAsync(job.ContentObjectId, inputPath, ct);

            var representation = await representationService.CreateRepresentationAsync(
                new CreateStreamingRepresentationRequest
                {
                    JobId = jobId,
                    Codec = job.IsVideo ? StreamCodec.H264 : StreamCodec.Opus
                }, ct);

            representationId = representation.Id;

            await representationService.MarkProcessingAsync(representation.Id, ct);

            LogTranspilationRunning(logger, jobId, job.IsVideo);

            TranspilationOutput output = job.IsVideo
                ? await videoTranspilation.TranspileAsync(inputPath, repDir, ct)
                : await audioTranspilation.TranspileAsync(inputPath, repDir, ct);

            var segmentPrefix = $"{job.ContentObjectId}/{mediaDir}/{codecStr}";

            LogUploadingOutput(logger, jobId, segmentPrefix);

            await storage.UploadStreamingOutputAsync(output.RootDirectory, segmentPrefix, ct);

            await representationService.MarkReadyAsync(representation.Id, segmentPrefix, ct);

            await jobService.UpdateStatusAsync(
                jobId, TranspilationStatus.Ready, progress: 100, ct: ct);

            LogJobCompleted(logger, jobId);
        }
        catch (Exception ex)
        {
            LogJobFailed(logger, ex, jobId);

            if (representationId.HasValue)
            {
                try
                {
                    await representationService.MarkFailedAsync(representationId.Value, ct);
                }
                catch (Exception cleanupEx)
                {
                    LogRepresentationMarkFailedError(logger, cleanupEx, job.Id, representationId.Value);
                }
            }

            try
            {
                await jobService.UpdateStatusAsync(
                    jobId,
                    TranspilationStatus.Failed,
                    errorDetail: ex.Message,
                    ct: ct);
            }
            catch (Exception cleanupEx)
            {
                LogJobMarkFailedError(logger, cleanupEx, jobId);
            }
        }
        finally
        {
            if (Directory.Exists(jobDir))
            {
                try
                {
                    Directory.Delete(jobDir, recursive: true);
                    LogLocalOutputCleaned(logger, jobId, jobDir);
                }
                catch (Exception cleanupEx)
                {
                    LogLocalOutputCleanupFailed(logger, cleanupEx, jobId, jobDir);
                }
            }
        }
    }
}