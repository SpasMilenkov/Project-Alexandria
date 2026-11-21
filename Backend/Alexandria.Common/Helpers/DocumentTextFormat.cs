namespace Alexandria.Common.Helpers;

/// <summary>
/// How a document stores its body text, which decides the reader.
/// </summary>
public enum DocumentTextFormat
{
    Pdf,
    WordprocessingMl,
    PresentationMl,
    SpreadsheetMl,
    OpenDocument,
    OpenDocumentSpreadsheet,
    Epub,
    PlainText
}