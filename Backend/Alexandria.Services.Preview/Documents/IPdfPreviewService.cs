using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Preview.Documents;

public interface IPdfPreviewService
{
    public Task<string> GeneratePreviewAsync(string inputPath, FileCategory fileCategory, CancellationToken ct);

    /// <summary>
    /// Generates a PDF preview of the first 5 pages from a Word document
    /// </summary>
    public Task<string> GenerateWordPreviewAsync(string inputPath, CancellationToken ct);

    /// <summary>
    /// Generates a PDF preview of the first 5 slides from a PowerPoint presentation
    /// </summary>
    public Task<string> GeneratePowerPointPreviewAsync(string inputPath, CancellationToken ct);

    /// <summary>
    /// Generates a PDF preview of the first 20 rows from the first sheet of an Excel workbook
    /// Note: LibreOffice doesn't support row-level filtering via CLI, so this creates a temporary
    /// file with only the first 20 rows, then converts that to PDF
    /// </summary>
    public Task<string> GenerateExcelPreviewAsync(string inputPath, CancellationToken ct);
}