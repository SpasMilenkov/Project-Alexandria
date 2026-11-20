using Alexandria.Common.Exceptions.Preview.Media;

namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Thrown when <c>pdftotext</c> cannot read a PDF for a reason other than a missing password,
/// usually because the file is damaged or not a PDF at all.
/// </summary>
public class PdfTextException : PreviewProcessException
{
    public int ExitCode { get; }
    public string ProcessError { get; }

    public PdfTextException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"pdftotext failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }
}