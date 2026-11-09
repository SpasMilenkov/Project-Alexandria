using Alexandria.Common.Exceptions.Preview.Media;

namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Thrown when <c>pdfinfo</c> cannot read a PDF for a reason other than a missing password,
/// usually because the file is damaged or not a PDF at all.
/// </summary>
public class PdfInfoException : PreviewProcessException
{
    public int ExitCode { get; }
    public string ProcessError { get; }

    public PdfInfoException(string inputPath, int exitCode, string processError)
        : base(inputPath, $"pdfinfo failed with exit code {exitCode} for '{inputPath}': {processError}")
    {
        ExitCode = exitCode;
        ProcessError = processError;
    }
}
