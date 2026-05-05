using Alexandria.Common.Exceptions.Preview.Documents;

namespace Alexandria.Common.Exceptions.Preview.Media;

/// <summary>
/// Thrown when an ffmpeg operation fails, either because the process could not be started,
/// exited with a non-zero code, or did not produce its expected output file.
/// The <see cref="Operation"/> property identifies which stage failed.
/// </summary>
public class FfmpegException : MediaProcessException
{
    public string Operation { get; }
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    /// <summary>
    /// Used when the ffmpeg process could not be started at all.
    /// </summary>
    public FfmpegException(string inputPath, string operation)
        : base(inputPath, $"Failed to start ffmpeg process for {operation} on '{inputPath}'.")
    {
        Operation = operation;
    }

    /// <summary>
    /// Used when ffmpeg started but exited with a non-zero code.
    /// </summary>
    public FfmpegException(string inputPath, string operation, int exitCode, string processError)
        : base(inputPath, $"ffmpeg {operation} failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        Operation = operation;
        ExitCode = exitCode;
        ProcessError = processError;
    }

    /// <summary>
    /// Used when ffmpeg appeared to succeed but did not produce the expected output file.
    /// </summary>
    public FfmpegException(string inputPath, string operation, string expectedOutput)
        : base(inputPath, $"ffmpeg {operation} did not create expected output '{expectedOutput}' for '{inputPath}'.")
    {
        Operation = operation;
    }
}