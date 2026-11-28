using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

/// <summary>The transcode a file version already has: the rungs it asked for and where it stands.</summary>
public sealed record ExistingTranscode
{
    public required Guid TranspilationId { get; init; }
    public required Guid UserId { get; init; }
    public required bool IsVideo { get; init; }
    public required JobStatus Status { get; init; }
    public required AudioRung[] AudioRungs { get; init; }
    public required VideoRung[] VideoRungs { get; init; }
}
