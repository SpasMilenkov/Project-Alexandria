using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Jobs;

public sealed record AdminJobDto(
    Guid Id,
    JobType Type,
    JobStatus Status,
    int RetryCount,
    string? ErrorDetail,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? UpdatedAt);
