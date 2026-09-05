using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Dto.Extensions;

public static class TranspilationJobExtensions
{
    public static TranspilationJobDto ToDto(this TranspilationJob transpilation)
    {
        if (transpilation.Job is null)
            throw new InvalidOperationException("Transpilation can't be mapped when Job is null");

        return new TranspilationJobDto
        {
            Id = transpilation.Id,
            VersionId = transpilation.VersionId,
            Status = transpilation.Job.Status,
            IsVideo = transpilation.IsVideo,
            ProgressPercent = transpilation.Job.ProgressPercent,
            RetryCount = transpilation.Job.RetryCount,
            ErrorDetail = transpilation.Job.ErrorDetail,
            StartedAt = transpilation.Job.StartedAt,
            CompletedAt = transpilation.Job.CompletedAt,
            CreatedAt = transpilation.CreatedAt,
            AudioRungs = transpilation.AudioRungs,
            VideoRungs = transpilation.VideoRungs,
            Representations = transpilation.Representations
                .Select(r => r.ToResponse())
                .ToList()
                .AsReadOnly()
        };
    }
}