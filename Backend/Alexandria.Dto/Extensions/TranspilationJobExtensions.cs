using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Dto.Extensions;

public static class TranspilationJobExtensions
{
    public static TranspilationJobResponse ToResponse(this TranspilationJob job)
        => new()
        {
            Id = job.Id,
            VersionId = job.VersionId,
            Status = job.Status,
            IsVideo = job.IsVideo,
            ProgressPercent = job.ProgressPercent,
            RetryCount = job.RetryCount,
            ErrorDetail = job.ErrorDetail,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            CreatedAt = job.CreatedAt,
            Representations = job.Representations
                .Select(r => r.ToResponse())
                .ToList()
                .AsReadOnly()
        };
}