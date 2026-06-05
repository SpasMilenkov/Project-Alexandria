namespace Alexandria.Common.Exceptions.Transpilation;

public class TranspilationFfprobeException : Exception
{
    public string InputPath { get; }
    public int? ExitCode { get; }
    public string? Stderr { get; }

    public TranspilationFfprobeException(string inputPath)
        : base($"ffprobe failed to start on '{inputPath}'.")
    {
        InputPath = inputPath;
    }

    public TranspilationFfprobeException(string inputPath, int exitCode, string stderr)
        : base($"ffprobe failed on '{inputPath}' (exit {exitCode}): {stderr}")
    {
        InputPath = inputPath;
        ExitCode = exitCode;
        Stderr = stderr;
    }

    public TranspilationFfprobeException(string inputPath, string message)
        : base(message)
    {
        InputPath = inputPath;
    }
}