using Alexandria.Common;
using Alexandria.Common.Helpers;
using Alexandria.Common.Mime;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Microsoft.Extensions.Logging;
using File = System.IO.File;

namespace Alexandria.Services.Preview.Documents.Metadata;

public sealed partial class DocumentMetadataService(
    IUnitOfWork unitOfWork,
    ILogger<DocumentMetadataService> logger) : IDocumentMetadataService
{
    private const string OpenDocumentPresentation = "application/vnd.oasis.opendocument.presentation";

    /// <inheritdoc/>
    public async Task<DocumentMetadata> ExtractAndStoreAsync(Guid versionId, string mimeType, string filePath,
        CancellationToken ct = default)
    {
        var format = DocumentMetadataFileTypes.FormatOf(mimeType)
                     ?? throw new ArgumentException($"No document metadata reader for '{mimeType}'.", nameof(mimeType));

        var fields = await ReadAsync(format, mimeType, filePath, ct);
        var metadata = DocumentMetadataMapper.ToEntity(versionId, mimeType, fields);

        await unitOfWork.DocumentMetadata.UpsertAsync(metadata, ct);

        LogStored(logger, versionId, format.ToString(), fields.Values.Count, metadata.IsPasswordProtected);

        return metadata;
    }

    private async Task<DocumentFields> ReadAsync(
        DocumentMetadataFormat format,
        string mimeType,
        string filePath,
        CancellationToken ct)
    {
        if (format == DocumentMetadataFormat.Pdf)
            return await PdfInfoReader.ReadAsync(filePath, logger, ct);

        // Zip, XML and binary header parsing is synchronous, so it runs off the caller's thread.
        return await Task.Run(() =>
        {
            using var source = File.OpenRead(filePath);

            if (format == DocumentMetadataFormat.OfficeOpenXml)
                return OfficeOpenXmlReader.Read(source, logger);

            if (format == DocumentMetadataFormat.Epub)
                return EpubReader.Read(source, logger);

            if (format == DocumentMetadataFormat.Mobi)
                return MobiReader.Read(source, logger);

            var isPresentation = MimeAliases.Canonicalize(mimeType) == OpenDocumentPresentation;

            return OpenDocumentReader.Read(source, isPresentation, logger);
        }, ct);
    }
}
