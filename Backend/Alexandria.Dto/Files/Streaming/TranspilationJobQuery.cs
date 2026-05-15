using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public sealed class TranspilationJobQuery
{
    public Guid? UserId { get; init; }
    public TranspilationStatus? Status { get; init; }
    public bool? IsVideo { get; init; }
    public Guid? ContentObjectId { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public DateTime? CompletedAfter { get; init; }
    public DateTime? CompletedBefore { get; init; }
    public int? MinRetryCount { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
    public bool IsSystem { get; init; } = false;
}