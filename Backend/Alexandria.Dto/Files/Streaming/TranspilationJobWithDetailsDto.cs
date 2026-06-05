using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public class TranspilationJobWithDetailsDto
{
    public Guid Id { get; init; }
    public Guid VersionId { get; init; }
    public TranspilationStatus Status { get; init; }
    public string FileName { get; set; }
    public int VersionNumber { get; set; }
    public bool IsVideo { get; init; }
    public int ProgressPercent { get; init; }
    public int RetryCount { get; init; }
    public string? ErrorDetail { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public VideoRung[] VideoRungs { get; set; } = [];
    public AudioRung[] AudioRungs { get; set; } = [];
    public IReadOnlyList<StreamingRepresentationDto> Representations { get; init; } = [];
}