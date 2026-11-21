using Alexandria.Dto.Files;

namespace Alexandria.Common.Services;

public interface IDocumentTextService
{
    /// <summary>
    /// Reads a document's body text. Nothing is stored, so the caller decides where the result goes.
    /// </summary>
    /// <param name="filePath">A local copy of the document, which <c>pdftotext</c> needs for PDFs.</param>
    /// <exception cref="ArgumentException">The type has no reader; check
    /// <see cref="Helpers.DocumentTextFileTypes.FormatOf"/> first.</exception>
    Task<DocumentTextResult> ExtractAsync(string mimeType, string filePath, CancellationToken ct = default);
}