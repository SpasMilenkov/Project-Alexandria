using System.Diagnostics;
using Alexandria.Common.Exceptions.Preview.Documents;
using Alexandria.Data.Models.Enumerators;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents;

public partial class PdfPreviewService(ILogger<PdfPreviewService> logger) : IPdfPreviewService
{
    /// <inheritdoc/>
    public async Task<string> GeneratePreviewAsync(string inputPath, FileCategory fileCategory, CancellationToken ct)
    {
        LogGeneratingPreview(logger, inputPath, fileCategory.ToString());
        try
        {
            var output = fileCategory switch
            {
                FileCategory.Document => await GenerateWordPreviewAsync(inputPath, ct),
                FileCategory.Presentation => await GeneratePowerPointPreviewAsync(inputPath, ct),
                FileCategory.Spreadsheet => await GenerateExcelPreviewAsync(inputPath, ct),
                FileCategory.Pdf => await TrimPdfWithGhostscriptAsync(inputPath, 5, ct),
                _ => await GenerateFullPdfAsync(inputPath, ct)
            };

            LogPreviewGenerated(logger, output);
            return output;
        }
        catch (LibreOfficeException)
        {
            // LibreOffice itself is broken, the fallback will also fail, so re-throw
            // rather than masking the real failure with a confusing second exception.
            throw;
        }
        catch (Exception ex)
        {
            LogPreviewFallback(logger, ex, inputPath);
            return await GenerateFullPdfAsync(inputPath, ct);
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateWordPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new LibreOfficeException(inputPath, expectedOutput);

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePowerPointPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new LibreOfficeException(inputPath, expectedOutput);

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateExcelPreviewAsync(string inputPath, CancellationToken ct)
    {
        var tempExcelPath = Path.Combine(
            Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(inputPath)}_temp.xlsx");

        var outputDir = Path.GetTempPath();
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
        var outputDir = Path.GetTempPath();
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

        var psi = new ProcessStartInfo
        {
            FileName = "libreoffice",
            Arguments = $"--headless --convert-to pdf --outdir \"{outputDir}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogLibreOfficeStartFailed(logger, inputPath);
            throw new LibreOfficeException(inputPath);
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogLibreOfficeFailed(logger, process.ExitCode, inputPath, error);
            throw new LibreOfficeException(inputPath, process.ExitCode, error);
        }

        LogLibreOfficeCompleted(logger, inputPath);
    }

    private async Task<string> TrimPdfWithGhostscriptAsync(string inputPdfPath, int maxPages, CancellationToken ct)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPdfPath) ?? Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(inputPdfPath)}_preview.pdf");

        LogGhostscriptStarting(logger, inputPdfPath, maxPages);

        var psi = new ProcessStartInfo
        {
            FileName = "gs",
            Arguments = $"-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite " +
                        $"-dFirstPage=1 -dLastPage={maxPages} " +
                        $"-sOutputFile=\"{outputPath}\" \"{inputPdfPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            LogGhostscriptStartFailed(logger, inputPdfPath);
            throw new GhostscriptException(inputPdfPath);
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            LogGhostscriptFailed(logger, process.ExitCode, inputPdfPath, error);
            throw new GhostscriptException(inputPdfPath, process.ExitCode, error);
        }

        if (!File.Exists(outputPath))
            throw new GhostscriptException(inputPdfPath, outputPath);

        if (inputPdfPath != outputPath && File.Exists(inputPdfPath))
            File.Delete(inputPdfPath);

        LogGhostscriptCompleted(logger, outputPath);
        return outputPath;
    }
}