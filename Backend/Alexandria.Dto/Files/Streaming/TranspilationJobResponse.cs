using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public sealed class TranspilationJobResponse
{
    public Guid Id { get; init; }
    public Guid VersionId { get; init; }
    public TranspilationStatus Status { get; init; }
    public bool IsVideo { get; init; }
    public int ProgressPercent { get; init; }
    public int RetryCount { get; init; }
    public string? ErrorDetail { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<StreamingRepresentationResponse> Representations { get; init; } = [];
}