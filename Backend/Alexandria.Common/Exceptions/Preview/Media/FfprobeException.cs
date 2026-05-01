using Alexandria.Common.Exceptions.Preview.Documents;

namespace Alexandria.Common.Exceptions.Preview.Media;

/// <summary>
/// Thrown when ffprobe fails to analyze a media file, either because the process
/// could not be started, exited with a non-zero code, or produced unparseable output.
/// </summary>
public class FfprobeException : MediaProcessException
{
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    /// <summary>
    /// Used when the ffprobe process could not be started at all.
    /// </summary>
    public FfprobeException(string inputPath)
        : base(inputPath, $"Failed to start ffprobe process for '{inputPath}'.")
    {
    }

    /// <summary>
    /// Used when ffprobe started but exited with a non-zero code.
    /// </summary>
    public FfprobeException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"ffprobe failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }

    /// <summary>
    /// Used when ffprobe succeeded but its output could not be parsed into usable metadata.
    /// </summary>
    public FfprobeException(string inputPath, string message)
        : base(inputPath, message)
    {
    }
}