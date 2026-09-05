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
        var claimed = await unitOfWork.Jobs.TryClaimJobAsync(jobId, ct);
        if (!claimed)
            return;

        var transpilation = await unitOfWork.TranspilationJobs.GetByJobIdAsync(jobId, ct);
        if (transpilation is null) throw new TranspilationJobNotFoundException(jobId);

        var mediaDir = transpilation.IsVideo ? "v" : "a";
        var codecStr = transpilation.IsVideo ? "h264" : "opus";
        var jobDir = Path.Combine(Path.GetTempPath(), jobId.ToString());
        var repDir = Path.Combine(jobDir, mediaDir, codecStr);
        var inputPath = Path.Combine(jobDir, "source");

        List<Guid> representationIds = [];

        try
        {
            Directory.CreateDirectory(repDir);

            LogDownloadingSource(logger, jobId, transpilation.VersionId);

            var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(v => v.Id == transpilation.VersionId, ct) ??
                          throw new VersionNotFoundException();

            await storage.DownloadContentObjectAsync(version.ContentObjectId, inputPath, ct);

            LogTranspilationRunning(logger, jobId, transpilation.IsVideo);

            if (!string.IsNullOrEmpty(transpilation.SegmentPrefix))
            {
                LogCleaningUpOldSegments(logger, jobId, transpilation.SegmentPrefix);
                await storage.DeleteStreamingOutputByPrefixAsync(transpilation.SegmentPrefix, ct);
                await representationService.DeleteByTranspilationIdAsync(transpilation.Id, ct);
            }

            var output = transpilation.IsVideo
                ? await videoTranspilation.TranspileAsync(jobId, inputPath, repDir, transpilation.VideoRungs, ct)
                : await audioTranspilation.TranspileAsync(jobId, inputPath, repDir, transpilation.AudioRungs, ct);

            var segmentPrefix = $"{transpilation.VersionId}/{mediaDir}/{codecStr}";

            LogUploadingOutput(logger, jobId, segmentPrefix);
            await storage.UploadStreamingOutputAsync(output.RootDirectory, segmentPrefix, ct);

            var representations = await representationService.CreateRepresentationsAsync(
                output.Lanes.Select(lane => new CreateStreamingRepresentationRequest
                {
                    TranspilationId = transpilation.Id,
                    Codec = lane.Codec,
                    BitrateKbps = lane.BitrateKbps,
                    Width = lane.Width,
                    Height = lane.Height
                }).ToList(), ct);

            representationIds = representations.Select(r => r.Id).ToList();

            await representationService.MarkAllReadyAsync(representationIds, ct);

            // Persist the TranspilationJob-specific field before flipping Job.Status,
            // so a Ready status is never observable before SegmentPrefix is in place.
            transpilation.SegmentPrefix = segmentPrefix;
            await unitOfWork.SaveChangesAsync(ct);

            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Ready, 100, ct: ct);

            LogJobCompleted(logger, jobId);
        }
        catch (TranspilationCancelledException)
        {
            await representationService.MarkAllFailedAsync(representationIds, ct);
            await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Cancelled, ct: ct);
        }
        catch (Exception ex)
        {
            if (representationIds.Count > 0)
                try
                {
                    await representationService.MarkAllFailedAsync(representationIds, ct);
                }
                catch (Exception cleanupEx)
                {
                    LogRepresentationMarkFailedError(logger, cleanupEx, jobId);
                }

            try
            {
                await unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: ex.Message, ct: ct);
            }
            catch (Exception cleanupEx)
            {
                LogJobMarkFailedError(logger, cleanupEx, jobId);
            }
        }
        finally
        {
            if (Directory.Exists(jobDir))
                try
                {
                    Directory.Delete(jobDir, true);
                    LogLocalOutputCleaned(logger, jobId, jobDir);
                }
                catch (Exception cleanupEx)
                {
                    LogLocalOutputCleanupFailed(logger, cleanupEx, jobId, jobDir);
                }
        }
    }
}