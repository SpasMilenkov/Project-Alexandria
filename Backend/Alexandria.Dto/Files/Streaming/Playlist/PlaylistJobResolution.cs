namespace Alexandria.Dto.Files.Streaming.Playlist;

/// <summary>
/// One viable transcode for the resolver: a live Ready job with a Ready
/// representation, on a live version of a live file. Fetched owner-wide in one
/// query; the pure selection helper picks the winner per file.
/// </summary>
public sealed record ResolvableTranscodeRow(
    Guid JobId,
    Guid FileId,
    Guid VersionId,
    DateTime? CompletedAt,
    Guid? CurrentVersionId);

/// <summary>One candidate job for a single file's selection.</summary>
public sealed record PlaylistJobCandidate(
    Guid JobId,
    Guid VersionId,
    DateTime? CompletedAt);

/// <summary>
/// Winning job per file. Files with no viable transcode are absent, never null.
/// </summary>
public sealed record PlaylistJobResolution(
    Guid FileId,
    Guid TranspilationJobId);