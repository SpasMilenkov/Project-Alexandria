using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Services;

/// <summary>
/// Shared claim/complete wrapper for preview workers. Resolving the
/// <see cref="PreviewJob"/> from the queue payload keeps every preview worker
/// (image, media, document) on the same lifecycle without duplicating it.
/// </summary>
public static class PreviewJobLifecycle
{
    /// <summary>
    /// Resolves the preview job and atomically claims its generic job.
    /// Returns null when another delivery already claimed it — the caller
    /// acks that delivery silently (D2: already-completed/superseded).
    /// Throws when the job id is unknown (dead-letter, like before).
    /// </summary>
    public static async Task<PreviewJob?> TryClaimAsync(
        IUnitOfWork unitOfWork, Guid jobId, CancellationToken ct = default)
    {
        var previewJob = await unitOfWork.PreviewJobs.GetByJobIdAsync(jobId, ct)
                         ?? throw new InvalidOperationException($"Preview job with ID {jobId} does not exist.");

        if (!await unitOfWork.Jobs.TryClaimJobAsync(jobId, ct))
            return null;

        return previewJob;
    }

    public static Task CompleteAsync(IUnitOfWork unitOfWork, Guid jobId, CancellationToken ct = default)
        => unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Ready, progress: 100, ct: ct);

    public static Task FailAsync(
        IUnitOfWork unitOfWork, Guid jobId, string errorDetail, CancellationToken ct = default)
        => unitOfWork.Jobs.UpdateStatusAsync(jobId, JobStatus.Failed, errorDetail: errorDetail, ct: ct);
}