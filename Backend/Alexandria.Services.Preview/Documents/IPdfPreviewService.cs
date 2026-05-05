using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Preview.Documents;

/// <summary>
/// Generates PDF preview files from various document formats by delegating to
/// LibreOffice (for conversion) and Ghostscript (for page trimming).
/// All methods return the path to a temporary PDF file on disk; callers are
/// responsible for deleting it after use.
/// </summary>
public interface IPdfPreviewService
{
    /// <summary>
    /// Generates a format-appropriate PDF preview for the given file, routing to the
    /// most specific preview strategy based on <paramref name="fileCategory"/>.
    /// Falls back to a full PDF conversion if the specialized strategy fails.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source file on disk.</param>
    /// <param name="fileCategory">
    /// The category of the source file, used to select the preview strategy.
    /// <see cref="FileCategory.Document"/> and <see cref="FileCategory.Presentation"/>
    /// produce a 5-page preview; <see cref="FileCategory.Spreadsheet"/> produces a
    /// 20-row preview of the first sheet; <see cref="FileCategory.Pdf"/> trims the
    /// existing PDF to 5 pages.
    /// </param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>Absolute path to the generated preview PDF in the system temp directory.</returns>
    Task<string> GeneratePreviewAsync(string inputPath, FileCategory fileCategory, CancellationToken ct);

    /// <summary>
    /// Converts a Word document to PDF via LibreOffice, then trims the result to
    /// the first 5 pages using Ghostscript.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source <c>.docx</c> / <c>.doc</c> file.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>Absolute path to the generated preview PDF in the system temp directory.</returns>
    /// <exception cref="Exception">
    /// Thrown when LibreOffice or Ghostscript exits with a non-zero code,
    /// or when either process fails to produce its expected output file.
    /// </exception>
    Task<string> GenerateWordPreviewAsync(string inputPath, CancellationToken ct);

    /// <summary>
    /// Converts a PowerPoint presentation to PDF via LibreOffice, then trims the
    /// result to the first 5 slides using Ghostscript.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source <c>.pptx</c> / <c>.ppt</c> file.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>Absolute path to the generated preview PDF in the system temp directory.</returns>
    /// <exception cref="Exception">
    /// Thrown when LibreOffice or Ghostscript exits with a non-zero code,
    /// or when either process fails to produce its expected output file.
    /// </exception>
    Task<string> GeneratePowerPointPreviewAsync(string inputPath, CancellationToken ct);

    /// <summary>
    /// Produces a PDF preview of the first 20 rows of the first sheet in an Excel workbook.
    /// Because LibreOffice has no CLI option for row-level filtering, the workbook is first
    /// trimmed in-memory with ClosedXML and saved to a temporary file, which is then converted
    /// to PDF and cleaned up.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source <c>.xlsx</c> file.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>Absolute path to the generated preview PDF in the system temp directory.</returns>
    /// <exception cref="Exception">
    /// Thrown when LibreOffice exits with a non-zero code, or when it fails to produce
    /// its expected output file.
    /// </exception>
    Task<string> GenerateExcelPreviewAsync(string inputPath, CancellationToken ct);
}