using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public sealed class TranspilationJobQuery
{
    public Guid? UserId { get; init; }
    public TranspilationStatus? Status { get; init; }
    public bool? IsVideo { get; init; }
    public Guid? ContentObjectId { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public DateTimeOffset? CreatedBefore { get; init; }
    public DateTimeOffset? CompletedAfter { get; init; }
    public DateTimeOffset? CompletedBefore { get; init; }
    public int? MinRetryCount { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
    public bool IsSystem { get; init; } = false;
}