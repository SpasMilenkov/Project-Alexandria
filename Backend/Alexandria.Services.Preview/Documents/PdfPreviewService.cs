using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Preview;
using Alexandria.Common.Exceptions.Preview.Documents;
using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents;

public partial class PdfPreviewService(ILogger<PdfPreviewService> logger) : IPdfPreviewService
{
    private const int ThumbnailMaxDimensionPx = 320;

    // The steps of one preview, fallback included, must total less than the job recovery stall
    // threshold, because preview jobs send no heartbeat and would otherwise be failed while running.
    private static readonly TimeSpan LibreOfficeTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GhostscriptTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PdftocairoTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc>
    public async Task<PdfPreviewResult> GeneratePreviewAsync(string inputPath, FileCategory fileCategory,
        CancellationToken ct)
    {
        LogGeneratingPreview(logger, inputPath, fileCategory.ToString());
        try
        {
            var previewPath = fileCategory switch
            {
                FileCategory.Document => await GenerateWordPreviewAsync(inputPath, ct),
                FileCategory.Presentation => await GeneratePowerPointPreviewAsync(inputPath, ct),
                FileCategory.Spreadsheet => await GenerateExcelPreviewAsync(inputPath, ct),
                FileCategory.Pdf => await TrimPdfWithGhostscriptAsync(inputPath, 5, ct),
                _ => await GenerateFullPdfAsync(inputPath, ct)
            };

            var thumbnailPath = await GenerateThumbnailAsync(previewPath, ct);

            LogPreviewGenerated(logger, previewPath);
            return new PdfPreviewResult(previewPath, thumbnailPath);
        }
        catch (LibreOfficeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPreviewFallback(logger, ex, inputPath);
            var fallbackPreview = await GenerateFullPdfAsync(inputPath, ct);
            var fallbackThumbnail = await GenerateThumbnailAsync(fallbackPreview, ct);
            return new PdfPreviewResult(fallbackPreview, fallbackThumbnail);
        }
    }


    /// <inheritdoc/>
    public async Task<string> GenerateThumbnailAsync(string inputPdfPath, CancellationToken ct)
    {
        var outputBasePath = Path.Combine(
            Path.GetDirectoryName(inputPdfPath) ?? Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(inputPdfPath)}_thumb");
        var expectedOutput = $"{outputBasePath}.png";

        LogThumbnailStarting(logger, inputPdfPath, ThumbnailMaxDimensionPx);

        try
        {
            await RunPdftocairoAsync(inputPdfPath, outputBasePath, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogThumbnailFallback(logger, ex, inputPdfPath);
            await RunGhostscriptThumbnailAsync(inputPdfPath, expectedOutput, ct);
        }

        if (!File.Exists(expectedOutput))
            throw new ThumbnailException(inputPdfPath, expectedOutput);

        LogThumbnailCompleted(logger, expectedOutput);
        return expectedOutput;
    }

    private async Task RunPdftocairoAsync(string inputPdfPath, string outputBasePath, CancellationToken ct)
    {
        var result = await ExternalProcess.RunAsync(
            "pdftocairo",
            $"-png -f 1 -l 1 -scale-to {ThumbnailMaxDimensionPx} -singlefile " +
            $"\"{inputPdfPath}\" \"{outputBasePath}\"",
            PdftocairoTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
            throw new ThumbnailException(inputPdfPath, result.ExitCode, result.StdErrTail);
    }

    private async Task RunGhostscriptThumbnailAsync(string inputPdfPath, string outputPath, CancellationToken ct)
    {
        var result = await ExternalProcess.RunAsync(
            "gs",
            $"-dNOPAUSE -dBATCH -dSAFER -dFirstPage=1 -dLastPage=1 " +
            $"-sDEVICE=png16m -r150 -dPDFFitPage " +
            $"-sOutputFile=\"{outputPath}\" \"{inputPdfPath}\"",
            GhostscriptTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
            throw new ThumbnailException(inputPdfPath, result.ExitCode, result.StdErrTail);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateWordPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new LibreOfficeException(inputPath, expectedOutput);

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePowerPointPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new LibreOfficeException(inputPath, expectedOutput);

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateExcelPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
        var tempExcelPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_temp.xlsx");
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_temp.pdf");
        var finalOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_preview.pdf");

        try
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(inputPath);
                var worksheet = workbook.Worksheet(1);
                var usedRange = worksheet.RangeUsed();

                if (usedRange != null)
                {
                    var maxRow = Math.Min(20, usedRange.RowCount());

                    if (usedRange.RowCount() > maxRow)
                        worksheet.Rows(maxRow + 1, usedRange.RowCount()).Delete();

                    LogExcelTrimmed(logger, maxRow, tempExcelPath);
                }

                workbook.SaveAs(tempExcelPath);
            }, ct);

            await ConvertToPdfWithLibreOffice(tempExcelPath, outputDir, ct);

            if (!File.Exists(expectedOutput))
                throw new LibreOfficeException(tempExcelPath, expectedOutput);

            File.Move(expectedOutput, finalOutput, overwrite: true);
            return finalOutput;
        }
        finally
        {
            if (File.Exists(tempExcelPath))
            {
                try
                {
                    File.Delete(tempExcelPath);
                    LogExcelTempDeleted(logger, tempExcelPath);
                }
                catch (Exception ex)
                {
                    LogExcelTempDeleteFailed(logger, ex, tempExcelPath);
                }
            }
        }
    }

    private async Task<string> GenerateFullPdfAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");
        var finalOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_preview.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new LibreOfficeException(inputPath, expectedOutput);

        File.Move(expectedOutput, finalOutput, overwrite: true);
        return finalOutput;
    }

    private async Task ConvertToPdfWithLibreOffice(string inputPath, string outputDir, CancellationToken ct)
    {
        LogLibreOfficeStarting(logger, inputPath, outputDir);

        ExternalProcessResult result;

        try
        {
            result = await ExternalProcess.RunAsync(
                "libreoffice",
                $"--headless --convert-to pdf --outdir \"{outputDir}\" \"{inputPath}\"",
                LibreOfficeTimeout,
                // LibreOffice keeps large working files in TMPDIR and cannot remove them if it is
                // killed, so they have to land somewhere that gets cleaned up without its help.
                new Dictionary<string, string> { ["TMPDIR"] = outputDir },
                logger,
                ct);
        }
        catch (ExternalProcessTimeoutException ex)
        {
            // Surfacing as LibreOfficeException skips the fallback, which would only run
            // LibreOffice on the same document again.
            throw new LibreOfficeException(inputPath, LibreOfficeTimeout, ex);
        }

        if (result.ExitCode != 0)
        {
            LogLibreOfficeFailed(logger, result.ExitCode, inputPath, result.StdErrTail);
            throw new LibreOfficeException(inputPath, result.ExitCode, result.StdErrTail);
        }

        LogLibreOfficeCompleted(logger, inputPath);
    }

    private async Task<string> TrimPdfWithGhostscriptAsync(string inputPdfPath, int maxPages, CancellationToken ct)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPdfPath) ?? Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(inputPdfPath)}_preview.pdf");

        LogGhostscriptStarting(logger, inputPdfPath, maxPages);

        var result = await ExternalProcess.RunAsync(
            "gs",
            $"-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite " +
            $"-dFirstPage=1 -dLastPage={maxPages} " +
            $"-sOutputFile=\"{outputPath}\" \"{inputPdfPath}\"",
            GhostscriptTimeout,
            logger,
            ct);

        if (result.ExitCode != 0)
        {
            LogGhostscriptFailed(logger, result.ExitCode, inputPdfPath, result.StdErrTail);
            throw new GhostscriptException(inputPdfPath, result.ExitCode, result.StdErrTail);
        }

        if (!File.Exists(outputPath))
            throw new GhostscriptException(inputPdfPath, outputPath);

        if (inputPdfPath != outputPath && File.Exists(inputPdfPath))
            File.Delete(inputPdfPath);

        LogGhostscriptCompleted(logger, outputPath);
        return outputPath;
    }
}