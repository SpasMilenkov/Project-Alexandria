using Alexandria.Common.Helpers;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Microsoft.Extensions.Logging;
using File = System.IO.File;

namespace Alexandria.Services.Preview.Documents.BodyText;

public sealed class DocumentTextService(ILogger<DocumentTextService> logger) : IDocumentTextService
{
    /// <inheritdoc/>
    public async Task<DocumentTextResult> ExtractAsync(string mimeType, string filePath, CancellationToken ct = default)
    {
        var format = DocumentTextFileTypes.FormatOf(mimeType)
                     ?? throw new ArgumentException($"No document text reader for '{mimeType}'.", nameof(mimeType));

        var collector = new TextCollector();
        var readerStatus = await ReadAsync(format, filePath, collector, ct);
        var status = Classify(readerStatus, collector);

        var result = IsLocked(status)
            ? new DocumentTextResult(status, string.Empty, IsTruncated: false)
            : new DocumentTextResult(status, collector.Build(), collector.IsTruncated);

        BodyTextLog.Extracted(logger, result.Text.Length, format.ToString(), status.ToString(), result.IsTruncated);

        return result;
    }

    private async Task<DocumentTextStatus?> ReadAsync(
        DocumentTextFormat format,
        string filePath,
        TextCollector collector,
        CancellationToken ct)
    {
        if (format == DocumentTextFormat.Pdf)
            return await PdfTextReader.ReadAsync(filePath, collector, logger, ct);

        // Zip, XML and text parsing is synchronous, so it runs off the caller's thread.
        return await Task.Run(() =>
        {
            using var source = File.OpenRead(filePath);

            return ReadLocal(format, source, collector, logger);
        }, ct);
    }

    private static DocumentTextStatus? ReadLocal(
        DocumentTextFormat format,
        Stream source,
        TextCollector collector,
        ILogger logger)
        => format switch
        {
            DocumentTextFormat.WordprocessingMl =>
                OfficeOpenXmlTextReader.ReadWordprocessing(source, collector, logger),
            DocumentTextFormat.PresentationMl => OfficeOpenXmlTextReader.ReadPresentation(source, collector, logger),
            DocumentTextFormat.SpreadsheetMl => OfficeOpenXmlTextReader.ReadSpreadsheet(source, collector, logger),
            DocumentTextFormat.OpenDocument => OpenDocumentTextReader.Read(source, collector, logger),
            DocumentTextFormat.OpenDocumentSpreadsheet => OpenDocumentTextReader.Read(source, collector, logger,
                isSpreadsheet: true),
            DocumentTextFormat.Epub => EpubTextReader.Read(source, collector, logger),
            DocumentTextFormat.PlainText => ReadPlainText(source, collector),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unhandled document text format.")
        };

    private static DocumentTextStatus? ReadPlainText(Stream source, TextCollector collector)
    {
        PlainTextReader.Read(source, collector);
        return null;
    }

    // A reader only names a status when the document's text was out of reach or suspect.
    internal static DocumentTextStatus Classify(DocumentTextStatus? readerStatus, TextCollector collector)
    {
        if (readerStatus is not null)
            return readerStatus.Value;

        if (collector.VisibleCharacters == 0)
            return DocumentTextStatus.Empty;

        return DocumentTextStatus.Extracted;
    }

    private static bool IsLocked(DocumentTextStatus status)
        => status is DocumentTextStatus.PasswordProtected or DocumentTextStatus.CopyProtected;
}