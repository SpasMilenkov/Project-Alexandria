namespace Alexandria.Dto.Files.Streaming;

/// <summary>Sweep one Transcode rule over the files created before <see cref="Cutoff"/>.</summary>
public sealed record TranscodeBackfillRequest
{
    public required Guid PolicyId { get; init; }
    public required Guid RuleId { get; init; }
    public required DateTimeOffset Cutoff { get; init; }
}
