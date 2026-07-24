using Alexandria.Common.Exceptions.Preview.Media;

namespace Alexandria.Common.Exceptions.Preview;

/// <summary>
/// Thrown when thumbnail generation fails, either because neither pdftocairo nor
/// the Ghostscript fallback could be started, both exited with a non-zero code,
/// or produced no output file.
/// </summary>
public class ThumbnailException : PreviewProcessException
{
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    /// <summary>
    /// Used when the thumbnail process could not be started at all.
    /// </summary>
    public ThumbnailException(string inputPath)
        : base(inputPath, $"Failed to start thumbnail generation process for '{inputPath}'.")
    {
    }

    /// <summary>
    /// Used when the thumbnail process started but exited with a non-zero code.
    /// </summary>
    public ThumbnailException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"Thumbnail generation failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }

    /// <summary>
    /// Used when the thumbnail process appeared to succeed but produced no output file.
    /// </summary>
    public ThumbnailException(string inputPath, string expectedOutput)
        : base(inputPath, $"Thumbnail generation did not create expected output '{expectedOutput}' for '{inputPath}'.")
    {
    }
}