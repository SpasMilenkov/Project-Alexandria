namespace Alexandria.Dto.Files.Streaming;

/// <summary>
/// Outcome of one rule sweep. Skipped covers files the trigger doesn't match, files whose
/// transcode already has every rung, cancelled transcodes and ones still running.
/// </summary>
public sealed record TranscodeBackfillResult
{
    public required int Candidates { get; init; }
    public required int Created { get; init; }
    public required int Requeued { get; init; }
    public required int Skipped { get; init; }
}
