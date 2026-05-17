using System.Data;
using Alexandria.Common;
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

        var job = await jobService.GetByIdAsync(jobId, ct);

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


            TranspilationOutput output = job.IsVideo
                ? await videoTranspilation.TranspileAsync(inputPath, repDir, ct)
                : await audioTranspilation.TranspileAsync(inputPath, repDir, ct);

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
        catch (Exception ex)
        {
            LogJobFailed(logger, ex, jobId);

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