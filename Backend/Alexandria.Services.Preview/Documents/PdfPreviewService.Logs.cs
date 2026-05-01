using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents;

public partial class PdfPreviewService
{
    // GeneratePreviewAsync

    [LoggerMessage(3001, LogLevel.Debug,
        "Generating preview for '{InputPath}' (category: {FileCategory})")]
    private static partial void LogGeneratingPreview(ILogger logger, string inputPath, string fileCategory);

    [LoggerMessage(3002, LogLevel.Warning,
        "Specialized preview failed for '{InputPath}', falling back to full PDF conversion")]
    private static partial void LogPreviewFallback(ILogger logger, Exception ex, string inputPath);

    [LoggerMessage(3003, LogLevel.Debug,
        "Preview generated successfully: '{OutputPath}'")]
    private static partial void LogPreviewGenerated(ILogger logger, string outputPath);

    // LibreOffice

    [LoggerMessage(3010, LogLevel.Debug,
        "Starting LibreOffice conversion: '{InputPath}' -> '{OutputDir}'")]
    private static partial void LogLibreOfficeStarting(ILogger logger, string inputPath, string outputDir);

    [LoggerMessage(3011, LogLevel.Debug,
        "LibreOffice conversion completed: '{InputPath}'")]
    private static partial void LogLibreOfficeCompleted(ILogger logger, string inputPath);

    [LoggerMessage(3012, LogLevel.Error,
        "LibreOffice process could not be started for '{InputPath}'")]
    private static partial void LogLibreOfficeStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(3013, LogLevel.Error,
        "LibreOffice exited with code {ExitCode} for '{InputPath}': {Error}")]
    private static partial void LogLibreOfficeFailed(ILogger logger, int exitCode, string inputPath, string error);

    // Ghostscript

    [LoggerMessage(3020, LogLevel.Debug,
        "Starting Ghostscript trim: '{InputPath}' -> max {MaxPages} pages")]
    private static partial void LogGhostscriptStarting(ILogger logger, string inputPath, int maxPages);

    [LoggerMessage(3021, LogLevel.Debug,
        "Ghostscript trim completed: '{OutputPath}'")]
    private static partial void LogGhostscriptCompleted(ILogger logger, string outputPath);

    [LoggerMessage(3022, LogLevel.Error,
        "Ghostscript process could not be started for '{InputPath}'")]
    private static partial void LogGhostscriptStartFailed(ILogger logger, string inputPath);

    [LoggerMessage(3023, LogLevel.Error,
        "Ghostscript exited with code {ExitCode} for '{InputPath}': {Error}")]
    private static partial void LogGhostscriptFailed(ILogger logger, int exitCode, string inputPath, string error);

    // Excel temp file

    [LoggerMessage(3030, LogLevel.Debug,
        "Trimmed Excel workbook to {RowCount} rows, saved temp file: '{TempPath}'")]
    private static partial void LogExcelTrimmed(ILogger logger, int rowCount, string tempPath);

    [LoggerMessage(3031, LogLevel.Debug,
        "Deleted temporary Excel file: '{TempPath}'")]
    private static partial void LogExcelTempDeleted(ILogger logger, string tempPath);

    [LoggerMessage(3032, LogLevel.Warning,
        "Failed to delete temporary Excel file: '{TempPath}'")]
    private static partial void LogExcelTempDeleteFailed(ILogger logger, Exception ex, string tempPath);
}