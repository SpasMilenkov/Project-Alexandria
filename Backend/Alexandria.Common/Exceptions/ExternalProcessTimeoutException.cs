namespace Alexandria.Common.Exceptions;

/// <summary>
/// Thrown when an external process outran its time limit and was killed.
/// </summary>
public class ExternalProcessTimeoutException(string fileName, TimeSpan timeout)
    : Exception($"'{fileName}' exceeded its {timeout} time limit and was killed.")
{
    public string FileName { get; } = fileName;
    public TimeSpan Timeout { get; } = timeout;
}
