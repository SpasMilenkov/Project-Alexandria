using Alexandria.Common.Exceptions.Preview.Documents;
using Alexandria.Common.Processes;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Reads a PDF's info dictionary and XMP packet through poppler's <c>pdfinfo</c>, which the
/// document worker already ships for previews.
/// </summary>
internal static class PdfInfoReader
{
    // Measured at well under a second even for a 2,000-page file, so this only catches a hang.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    // pdfinfo prints these labels in a fixed set. Matching only known labels keeps a title that
    // contains a line break from being read as a field of its own.
    private static readonly HashSet<string> KnownLabels = new(StringComparer.Ordinal)
    {
        "Title", "Subject", "Keywords", "Author", "Creator", "Producer", "CreationDate", "ModDate",
        "Custom Metadata", "Metadata Stream", "Tagged", "UserProperties", "Suspects", "Form",
        "JavaScript", "Pages", "Encrypted", "Page size", "Page rot", "File size", "Optimized",
        "PDF version"
    };

    // Free-text values can span lines, so a line without a known label continues these.
    private static readonly HashSet<string> MultiLineLabels = new(StringComparer.Ordinal)
    {
        "Title", "Subject", "Keywords", "Author", "Creator", "Producer"
    };

    public static async Task<DocumentFields> ReadAsync(string filePath, ILogger logger, CancellationToken ct)
    {
        var info = await ExternalProcess.RunAsync(
            "pdfinfo", $"-isodates -enc UTF-8 \"{filePath}\"", Timeout, logger, ct);

        if (info.ExitCode != 0)
        {
            // A PDF that needs a password to open shows nothing else, and asking again will
            // never change that, so it is a result rather than a failure.
            if (info.StdErrTail.Contains("Incorrect password", StringComparison.OrdinalIgnoreCase))
                return DocumentFields.PasswordProtected;

            throw new PdfInfoException(filePath, info.ExitCode, info.StdErrTail);
        }

        var fields = new DocumentFieldBag();
        ParseInfo(info.StdOut, fields);

        if (fields.Values.GetValueOrDefault("pdf:Metadata Stream") == "yes")
        {
            var xmp = await ExternalProcess.RunAsync("pdfinfo", $"-meta \"{filePath}\"", Timeout, logger, ct);

            if (xmp.ExitCode == 0)
                XmpReader.ReadInto(xmp.StdOut, fields, logger);
        }

        return fields.ToFields();
    }

    internal static void ParseInfo(string output, DocumentFieldBag fields)
    {
        string? currentLabel = null;
        var currentValue = new System.Text.StringBuilder();

        foreach (var line in output.Split('\n'))
        {
            var trimmedEnd = line.TrimEnd('\r');
            var colon = trimmedEnd.IndexOf(':');
            var label = colon > 0 ? trimmedEnd[..colon] : null;

            if (label is not null && KnownLabels.Contains(label))
            {
                Flush(fields, currentLabel, currentValue);
                currentLabel = label;
                currentValue.Clear().Append(trimmedEnd[(colon + 1)..].Trim());
                continue;
            }

            if (currentLabel is not null && MultiLineLabels.Contains(currentLabel))
                currentValue.Append('\n').Append(trimmedEnd);
        }

        Flush(fields, currentLabel, currentValue);
    }

    private static void Flush(DocumentFieldBag fields, string? label, System.Text.StringBuilder value)
    {
        if (label is null)
            return;

        fields.Add($"pdf:{label}", value.ToString());
    }
}
