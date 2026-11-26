namespace Alexandria.Services.Streaming;

/// <summary>
/// One ffmpeg run for a transcode job. <paramref name="ExpectedOutput"/> must exist afterwards
/// for the run to count as a success. Progress is reported only when
/// <paramref name="SourceDuration"/> is known.
/// </summary>
internal sealed record TranscodeRequest(
    Guid JobId,
    string InputPath,
    string Arguments,
    string Operation,
    string ExpectedOutput,
    TimeSpan? SourceDuration = null);
