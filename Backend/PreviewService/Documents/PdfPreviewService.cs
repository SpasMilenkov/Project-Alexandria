using System.Diagnostics;
using Models.Enumerators;

namespace PreviewService.Documents;

public class PdfPreviewService() : IPdfPreviewService
{
    /// <summary>
    /// Main entry point - routes to appropriate preview generation method based on file type
    /// Falls back to full PDF conversion if specialized preview fails
    /// </summary>
    public async Task<string> GeneratePreviewAsync(string inputPath, FileCategory fileCategory, CancellationToken ct)
    {
        try
        {
            return fileCategory switch
            {
                FileCategory.Document => await GenerateWordPreviewAsync(inputPath, ct),
                FileCategory.Presentation => await GeneratePowerPointPreviewAsync(inputPath, ct),
                FileCategory.Spreadsheet => await GenerateExcelPreviewAsync(inputPath, ct),
                FileCategory.Pdf => await TrimPdfWithGhostscriptAsync(inputPath, 5, ct),
                _ => await GenerateFullPdfAsync(inputPath, ct)
            };
        }
        catch
        {
            //TODO: Add proper lotgging in case of errors
            return await GenerateFullPdfAsync(inputPath, ct);
        }
    }

    /// <summary>
    /// Fallback method - generates full PDF without any page/row limits
    /// </summary>
    private async Task<string> GenerateFullPdfAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");
        var finalOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_preview.pdf");

        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new Exception($"LibreOffice did not create expected output: {expectedOutput}");

        File.Move(expectedOutput, finalOutput, overwrite: true);
        return finalOutput;
    }

    /// <summary>
    /// Generates a PDF preview of the first 5 pages from a Word document
    /// Strategy: Convert full document to PDF with LibreOffice, then trim to 5 pages with Ghostscript
    /// </summary>
    public async Task<string> GenerateWordPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");
        
        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new Exception($"LibreOffice did not create expected output: {expectedOutput}");

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <summary>
    /// Generates a PDF preview of the first 5 slides from a PowerPoint presentation
    /// Strategy: Convert full presentation to PDF with LibreOffice, then trim to 5 pages with Ghostscript
    /// </summary>
    public async Task<string> GeneratePowerPointPreviewAsync(string inputPath, CancellationToken ct)
    {
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");
        
        await ConvertToPdfWithLibreOffice(inputPath, outputDir, ct);

        if (!File.Exists(expectedOutput))
            throw new Exception($"LibreOffice did not create expected output: {expectedOutput}");

        return await TrimPdfWithGhostscriptAsync(expectedOutput, 5, ct);
    }

    /// <summary>
    /// Generates a PDF preview of the first 20 rows from the first sheet of an Excel workbook
    /// Strategy: Pre-trim the Excel file using ClosedXML, then convert to PDF with LibreOffice
    /// </summary>
    public async Task<string> GenerateExcelPreviewAsync(string inputPath, CancellationToken ct)
    {
        var tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(inputPath)}_temp.xlsx");
        var outputDir = Path.GetTempPath();
        var expectedOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_temp.pdf");
        var finalOutput = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}_preview.pdf");

        try
        {
            await Task.Run(() =>
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(inputPath);
                var worksheet = workbook.Worksheet(1);
                var usedRange = worksheet.RangeUsed();

                if (usedRange != null)
                {
                    var maxRow = Math.Min(20, usedRange.RowCount());
                    
                    // Delete rows beyond the first 20
                    if (usedRange.RowCount() > maxRow)
                    {
                        worksheet.Rows(maxRow + 1, usedRange.RowCount()).Delete();
                    }
                }

                workbook.SaveAs(tempExcelPath);
            }, ct);
            
            await ConvertToPdfWithLibreOffice(tempExcelPath, outputDir, ct);

            if (!File.Exists(expectedOutput))
                throw new Exception($"LibreOffice did not create expected output: {expectedOutput}");

            File.Move(expectedOutput, finalOutput, overwrite: true);
            return finalOutput;
        }
        finally
        {
            // Clean up temporary files
            if (File.Exists(tempExcelPath))
                File.Delete(tempExcelPath);
        }
    }

    /// <summary>
    /// Common method to convert any document to PDF using LibreOffice
    /// </summary>
    private async Task ConvertToPdfWithLibreOffice(string inputPath, string outputDir, CancellationToken ct)
    {
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
            throw new Exception("Failed to start LibreOffice process");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"LibreOffice failed with exit code {process.ExitCode}: {error}");
        }
    }

    /// <summary>
    /// Trims a PDF to the specified number of pages using Ghostscript
    /// Uses -dFirstPage and -dLastPage to extract page range
    /// </summary>
    private async Task<string> TrimPdfWithGhostscriptAsync(string inputPdfPath, int maxPages, CancellationToken ct)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPdfPath) ?? Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(inputPdfPath)}_preview.pdf"
        );

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
            throw new Exception("Failed to start Ghostscript process");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"Ghostscript failed with exit code {process.ExitCode}: {error}");
        }

        if (!File.Exists(outputPath))
            throw new Exception($"Ghostscript did not create expected output: {outputPath}");

        // Clean up the original full PDF if it's different from the output
        if (inputPdfPath != outputPath && File.Exists(inputPdfPath))
        {
            File.Delete(inputPdfPath);
        }

        return outputPath;
    }
}
