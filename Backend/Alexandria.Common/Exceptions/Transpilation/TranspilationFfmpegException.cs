namespace Alexandria.Common.Exceptions.Transpilation;

public class TranspilationFfmpegException : Exception
{
    public string InputPath { get; }
    public string Operation { get; }
    public int? ExitCode { get; }
    public string? Stderr { get; }

    public TranspilationFfmpegException(string inputPath, string operation)
        : base($"ffmpeg failed during '{operation}' on '{inputPath}'.")
    {
        InputPath = inputPath;
        Operation = operation;
    }

    public TranspilationFfmpegException(string inputPath, string operation, int exitCode, string stderr)
        : base($"ffmpeg failed during '{operation}' on '{inputPath}' (exit {exitCode}): {stderr}")
    {
        InputPath = inputPath;
        Operation = operation;
        ExitCode = exitCode;
        Stderr = stderr;
    }

    /// <summary>Use when the process exits 0 but the expected output file is absent.</summary>
    public TranspilationFfmpegException(string inputPath, string operation, string missingOutput)
        : base($"ffmpeg completed '{operation}' on '{inputPath}' but output '{missingOutput}' was not produced.")
    {
        InputPath = inputPath;
        Operation = operation;
    }
}