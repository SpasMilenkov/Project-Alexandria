using Alexandria.Common.Exceptions.Preview.Documents;
using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads a PDF's text layer through poppler's <c>pdftotext</c>, which the document worker already
/// ships for previews. The default reading order is kept rather than <c>-layout</c>: layout mode
/// pads columns with spaces and interleaves them line by line, which splits sentences apart.
/// </summary>
internal static class PdfTextReader
{
    // Large text-heavy files finish in seconds, so this only catches a hang.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Scanning apps often stamp a page number or their own name onto each page, so a scan is not
    /// always completely empty. Real text pages carry hundreds of characters.
    /// </summary>
    internal const int MinVisibleCharactersPerPage = 20;

    public static async Task<DocumentTextStatus?> ReadAsync(string filePath, TextCollector collector,
        ILogger logger, CancellationToken ct)
    {
        var result = await ExternalProcess.RunAsync(
            "pdftotext", $"-enc UTF-8 \"{filePath}\" -", Timeout, logger, ct);

        if (result.ExitCode != 0)
        {
            // A PDF that needs a password shows nothing, and asking again will never change that,
            // so it is a result rather than a failure.
            if (result.StdErrTail.Contains("Incorrect password", StringComparison.OrdinalIgnoreCase))
                return DocumentTextStatus.PasswordProtected;

            throw new PdfTextException(filePath, result.ExitCode, result.StdErrTail);
        }

        collector.Append(result.StdOut);

        return LooksScanned(CountPages(result.StdOut), collector.VisibleCharacters)
            ? DocumentTextStatus.NoTextLayer
            : null;
    }

    // pdftotext ends every page, including the last, with a form feed.
    internal static int CountPages(string output) => output.Count(c => c == '\f');

    internal static bool LooksScanned(int pages, int visibleCharacters)
        => pages > 0 && visibleCharacters < pages * MinVisibleCharactersPerPage;
}