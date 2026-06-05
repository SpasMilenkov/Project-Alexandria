using System.Data;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Directory = System.IO.Directory;

namespace Alexandria.Workers.MediaTranspilation.Handlers;

public partial class TranspilationJobHandler(
    ITranspilationJobService jobService,
    IStreamingRepresentationService representationService,
    IStorageService storage,
    IVideoTranspilationService videoTranspilation,
    IAudioTranspilationService audioTranspilation,
    IUnitOfWork unitOfWork,
    ILogger<TranspilationJobHandler> logger)
{
    public async Task HandleAsync(Guid jobId, CancellationToken ct = default)
    {
        var claimed = await jobService.TryClaimJobAsync(jobId, ct);
        if (!claimed)
            return;

        var job = await unitOfWork.TranspilationJobs.GetByIdAsync(jobId, ct);
        if (job is null) throw new TranspilationJobNotFoundException(jobId);

        var mediaDir = job.IsVideo ? "v" : "a";
        var codecStr = job.IsVideo ? "h264" : "opus";
        var jobDir = Path.Combine(Path.GetTempPath(), jobId.ToString());
        var repDir = Path.Combine(jobDir, mediaDir, codecStr);
        var inputPath = Path.Combine(jobDir, "source");

        List<Guid> representationIds = [];

        try
        {
            Directory.CreateDirectory(repDir);

            LogDownloadingSource(logger, jobId, job.VersionId);

            var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(v => v.Id == job.VersionId, ct) ??
                          throw new VersionNotFoundException();

            await storage.DownloadContentObjectAsync(version.ContentObjectId, inputPath, ct);

            LogTranspilationRunning(logger, jobId, job.IsVideo);

            if (!string.IsNullOrEmpty(job.SegmentPrefix))
            {
                LogCleaningUpOldSegments(logger, jobId, job.SegmentPrefix);
                await storage.DeleteStreamingOutputByPrefixAsync(job.SegmentPrefix, ct);
                await representationService.DeleteByJobIdAsync(jobId, ct);
            }

            TranspilationOutput output = job.IsVideo
                ? await videoTranspilation.TranspileAsync(jobId, inputPath, repDir, job.VideoRungs, ct)
                : await audioTranspilation.TranspileAsync(jobId, inputPath, repDir, job.AudioRungs, ct);

            var segmentPrefix = $"{job.VersionId}/{mediaDir}/{codecStr}";

            LogUploadingOutput(logger, jobId, segmentPrefix);
            await storage.UploadStreamingOutputAsync(output.RootDirectory, segmentPrefix, ct);

            var representations = await representationService.CreateRepresentationsAsync(
                output.Lanes.Select(lane => new CreateStreamingRepresentationRequest
                {
                    JobId = jobId,
                    Codec = lane.Codec,
                    BitrateKbps = lane.BitrateKbps,
                    Width = lane.Width,
                    Height = lane.Height
                }).ToList(), ct);

            representationIds = representations.Select(r => r.Id).ToList();

            await representationService.MarkAllReadyAsync(representationIds, ct);

            await jobService.UpdateStatusAsync(jobId, TranspilationStatus.Ready, progress: 100,
                segmentPrefix: segmentPrefix, ct: ct);

            LogJobCompleted(logger, jobId);
        }
        catch (TranspilationCancelledException)
        {
            await representationService.MarkAllFailedAsync(representationIds, ct);
            await jobService.UpdateStatusAsync(jobId, TranspilationStatus.Cancelled, ct: ct);
        }
        catch (Exception ex)
        {
            if (representationIds.Count > 0)
            {
                try
                {
                    await representationService.MarkAllFailedAsync(representationIds, ct);
                }
                catch (Exception cleanupEx)
                {
                    LogRepresentationMarkFailedError(logger, cleanupEx, jobId);
                }
            }

            try
            {
                await jobService.UpdateStatusAsync(jobId, TranspilationStatus.Failed, errorDetail: ex.Message, ct: ct);
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