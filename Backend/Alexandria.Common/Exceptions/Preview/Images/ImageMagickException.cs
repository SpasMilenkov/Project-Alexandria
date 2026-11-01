namespace Alexandria.Common.Exceptions.Preview.Images;

/// <summary>
/// Thrown when the sandboxed ImageMagick decoder fails, exceeds its limits or cannot be started.
/// </summary>
public class ImageMagickException : Exception
{
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    public ImageMagickException(string message)
        : base(message)
    {
    }

    public ImageMagickException(int exitCode, string processError)
        : base($"ImageMagick failed with exit code {exitCode}: {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }
}
