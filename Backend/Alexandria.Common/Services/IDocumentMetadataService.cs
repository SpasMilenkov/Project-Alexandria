using Alexandria.Data.Models;

namespace Alexandria.Common.Services;

public interface IDocumentMetadataService
{
    /// <summary>
    /// Reads the properties a document stores about itself and saves them for the version. Only
    /// the property parts are read, never the body text.
    /// </summary>
    /// <param name="filePath">A local copy of the version, which <c>pdfinfo</c> needs for PDFs.</param>
    /// <returns>The row that was stored.</returns>
    /// <exception cref="ArgumentException">The type has no reader; check
    /// <see cref="Helpers.DocumentMetadataFileTypes.FormatOf"/> first.</exception>
    Task<DocumentMetadata> ExtractAndStoreAsync(Guid versionId, string mimeType, string filePath, CancellationToken ct = default);
}
