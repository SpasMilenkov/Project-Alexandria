namespace Alexandria.Common.Exceptions.Preview.Media;

/// <summary>
/// Base exception for failures in external preview generation processes.
/// </summary>
public abstract class PreviewProcessException : Exception
{
    public string InputPath { get; }

    protected PreviewProcessException(string inputPath, string message)
        : base(message)
    {
        InputPath = inputPath;
    }

    protected PreviewProcessException(string inputPath, string message, Exception inner)
        : base(message, inner)
    {
        InputPath = inputPath;
    }
}