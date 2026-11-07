namespace Alexandria.Common.Exceptions;

/// <summary>
/// Thrown when an upload's stored bytes are missing or don't match what the client declared.
/// </summary>
public class UploadVerificationException : InvalidOperationException
{
    public UploadVerificationException(string message)
        : base(message)
    {
    }

    public UploadVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
