using Alexandria.Common.Exceptions.Preview.Media;

namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Thrown when Ghostscript fails to trim a PDF, either because the process
/// could not be started, exited with a non-zero code, or produced no output file.
/// </summary>
public class GhostscriptException : PreviewProcessException
{
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    /// <summary>
    /// Used when the Ghostscript process could not be started at all.
    /// </summary>
    public GhostscriptException(string inputPath)
        : base(inputPath, $"Failed to start Ghostscript process for '{inputPath}'.")
    {
    }

    /// <summary>
    /// Used when Ghostscript started but exited with a non-zero code.
    /// </summary>
    public GhostscriptException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"Ghostscript failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }

    /// <summary>
    /// Used when Ghostscript appeared to succeed but produced no output file.
    /// </summary>
    public GhostscriptException(string inputPath, string expectedOutput)
        : base(inputPath, $"Ghostscript did not create expected output '{expectedOutput}' for '{inputPath}'.")
    {
    }
}