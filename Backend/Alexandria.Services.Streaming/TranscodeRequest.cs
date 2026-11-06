namespace Alexandria.Services.Streaming;

/// <summary>
/// One ffmpeg run for a transcode job. <paramref name="ExpectedOutput"/> must exist afterwards
/// for the run to count as a success.
/// </summary>
internal sealed record TranscodeRequest(
    Guid JobId,
    string InputPath,
    string Arguments,
    string Operation,
    string ExpectedOutput);
