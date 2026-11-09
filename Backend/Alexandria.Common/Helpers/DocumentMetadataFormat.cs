namespace Alexandria.Common.Helpers;

/// <summary>
/// How a document stores its properties, which decides the reader.
/// </summary>
public enum DocumentMetadataFormat
{
    OfficeOpenXml,
    OpenDocument,
    Pdf,
    Epub,
    Mobi
}
