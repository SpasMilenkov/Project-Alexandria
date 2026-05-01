namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Base exception for failures in external media processing tools (ffprobe, ffmpeg).
/// </summary>
public abstract class MediaProcessException : Exception
{
    public string InputPath { get; }

    protected MediaProcessException(string inputPath, string message)
        : base(message)
    {
        InputPath = inputPath;
    }

    protected MediaProcessException(string inputPath, string message, Exception inner)
        : base(message, inner)
    {
        InputPath = inputPath;
    }
}