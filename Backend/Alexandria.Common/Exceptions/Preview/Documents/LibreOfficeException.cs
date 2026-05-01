using Alexandria.Common.Exceptions.Preview.Media;

namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Thrown when LibreOffice fails to convert a document to PDF, either because
/// the process could not be started or because it exited with a non-zero code.
/// </summary>
public class LibreOfficeException : PreviewProcessException
{
    public int? ExitCode { get; }
    public string? ProcessError { get; }

    /// <summary>
    /// Used when the LibreOffice process could not be started at all.
    /// </summary>
    public LibreOfficeException(string inputPath)
        : base(inputPath, $"Failed to start LibreOffice process for '{inputPath}'.")
    {
    }

    /// <summary>
    /// Used when LibreOffice started but exited with a non-zero code.
    /// </summary>
    public LibreOfficeException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"LibreOffice failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }

    /// <summary>
    /// Used when LibreOffice appeared to succeed but produced no output file.
    /// </summary>
    public LibreOfficeException(string inputPath, string expectedOutput)
        : base(inputPath, $"LibreOffice did not create expected output '{expectedOutput}' for '{inputPath}'.")
    {
    }
}