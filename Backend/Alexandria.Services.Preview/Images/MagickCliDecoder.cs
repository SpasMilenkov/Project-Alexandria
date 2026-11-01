using System.Diagnostics;
using System.Globalization;
using System.Text;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Preview.Images;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Preview.Images;

/// <summary>
/// Decodes formats libvips cannot load by running the ImageMagick CLI in a separate process
/// under kernel (<c>prlimit</c>) and ImageMagick resource limits. The result is a downscaled
/// PNG that goes back through the regular libvips pipeline.
/// </summary>
public sealed partial class MagickCliDecoder
{
    private const int MaxErrorBytes = 2048;
    private const int ProbeTimeoutSeconds = 10;

    // Only one child at a time: the limits below are per process, not per worker.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly Dictionary<string, string> Coders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/bmp"] = "BMP",
        ["image/x-bmp"] = "BMP",
        ["image/x-ms-bmp"] = "BMP",
        ["image/tga"] = "TGA",
        ["image/x-tga"] = "TGA",
        ["image/x-targa"] = "TGA",
        ["image/qoi"] = "QOI",
        ["image/x-qoi"] = "QOI",
        ["image/heic"] = "HEIC",
        ["image/heif"] = "HEIC",
        ["image/heic-sequence"] = "HEIC",
        ["image/heif-sequence"] = "HEIC",
        ["image/vnd.adobe.photoshop"] = "PSD",
        ["image/x-photoshop"] = "PSD",
        ["image/psd"] = "PSD",
        ["image/x-icon"] = "ICO",
        ["image/vnd.microsoft.icon"] = "ICO",
        ["image/x-pcx"] = "PCX",
        ["image/vnd.zbrush.pcx"] = "PCX",
        ["image/vnd.ms-dds"] = "DDS",
        ["image/x-dds"] = "DDS",
        ["image/jp2"] = "JP2",
        ["image/jpx"] = "JP2",
        ["image/tiff"] = "TIFF",
        ["image/x-portable-anymap"] = "PNM",
        ["image/x-portable-bitmap"] = "PBM",
        ["image/x-portable-graymap"] = "PGM",
        ["image/x-portable-pixmap"] = "PPM"
    };

    private readonly ImageMagickConfig _config;
    private readonly ILogger<MagickCliDecoder> _logger;
    private readonly Lazy<Task<bool>> _availability;

    public MagickCliDecoder(IOptions<ImagePreviewConfig> options, ILogger<MagickCliDecoder> logger)
    {
        _config = options.Value.Magick;
        _logger = logger;
        _availability = new Lazy<Task<bool>>(ProbeAsync);
    }

    /// <summary>
    /// The ImageMagick coder for a MIME type, or <c>null</c> when the type is not allowlisted.
    /// </summary>
    public static string? CoderFor(string? mimeType)
    {
        if (mimeType is null)
            return null;

        return Coders.GetValueOrDefault(mimeType);
    }

    public Task<bool> IsAvailableAsync() =>
        _config.Enabled ? _availability.Value : Task.FromResult(false);

    public async Task<byte[]> DecodeToPngAsync(Stream source, string coder, CancellationToken ct)
    {
        if (source.Length > _config.MaxInputBytes)
            throw new ImageMagickException(
                $"Source of {source.Length} bytes exceeds the ImageMagick input limit of {_config.MaxInputBytes} bytes.");

        await Gate.WaitAsync(ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            LogDecodeStarting(_logger, coder, source.Length);

            var result = await RunAsync(BuildDecodeArguments(_config, coder), source, timeout.Token, ct);

            if (result.ExitCode != 0)
                throw new ImageMagickException(result.ExitCode, result.Error);

            if (result.Output.Length == 0)
                throw new ImageMagickException("ImageMagick exited successfully but produced no output.");

            LogDecodeCompleted(_logger, coder, result.Output.Length);
            return result.Output;
        }
        finally
        {
            Gate.Release();
        }
    }

    internal static List<string> BuildDecodeArguments(ImageMagickConfig config, string coder)
    {
        var arguments = BuildPrlimitArguments(config);
        var size = config.IntermediateSize.ToString(CultureInfo.InvariantCulture);

        arguments.AddRange(
        [
            "-limit", "memory", config.MemoryLimit,
            "-limit", "map", config.MapLimit,
            "-limit", "disk", config.DiskLimit,
            "-limit", "area", config.AreaLimit,
            "-limit", "width", config.MaxDimension,
            "-limit", "height", config.MaxDimension,
            "-limit", "time", config.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            "-limit", "thread", "1",
            "-limit", "list-length", "64",

            // The explicit coder prefix pins the decoder so magic bytes cannot switch formats.
            $"{coder}:-[0]",
            "-auto-orient",
            "-thumbnail", $"{size}x{size}>",
            "-strip",
            "png:-"
        ]);

        return arguments;
    }

    private static List<string> BuildPrlimitArguments(ImageMagickConfig config) =>
    [
        $"--as={config.AddressSpaceBytes.ToString(CultureInfo.InvariantCulture)}",
        $"--cpu={config.CpuSeconds.ToString(CultureInfo.InvariantCulture)}",
        $"--fsize={config.MaxFileBytes.ToString(CultureInfo.InvariantCulture)}",
        "--nofile=64",
        "--",
        config.BinaryPath
    ];

    private async Task<bool> ProbeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ProbeTimeoutSeconds));
            var arguments = BuildPrlimitArguments(_config);
            arguments.Add("-version");

            var result = await RunAsync(arguments, null, timeout.Token, CancellationToken.None);
            if (result.ExitCode != 0)
            {
                LogUnavailable(_logger, result.Error);
                return false;
            }

            var version = Encoding.UTF8.GetString(result.Output).Split('\n')[0].Trim();
            LogAvailable(_logger, version);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogUnavailable(_logger, ex.Message);
            return false;
        }
    }

    private async Task<ProcessResult> RunAsync(
        List<string> arguments,
        Stream? input,
        CancellationToken token,
        CancellationToken callerToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _config.PrlimitPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.Environment["MAGICK_THREAD_LIMIT"] = "1";

        using var process = new Process();
        process.StartInfo = startInfo;

        if (!process.Start())
            throw new ImageMagickException("Failed to start the ImageMagick process.");

        try
        {
            var stdin = FeedAsync(input, process.StandardInput.BaseStream, token);
            var stderr = ReadAsync(process.StandardError.BaseStream, MaxErrorBytes, truncate: true, token);
            var output = await ReadAsync(process.StandardOutput.BaseStream, _config.MaxOutputBytes, truncate: false,
                token);

            await process.WaitForExitAsync(token);
            await stdin;

            var error = Encoding.UTF8.GetString(await stderr).Trim();
            return new ProcessResult(process.ExitCode, output, error);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            Kill(process);
            throw new ImageMagickException($"ImageMagick exceeded its {_config.TimeoutSeconds}s time limit.");
        }
        catch
        {
            Kill(process);
            throw;
        }
    }

    private static async Task FeedAsync(Stream? input, Stream stdin, CancellationToken token)
    {
        try
        {
            if (input is not null)
            {
                input.Position = 0;
                await input.CopyToAsync(stdin, token);
            }
        }
        catch (IOException)
        {
            // The child closed its stdin early (usually after rejecting the input); its exit code
            // and stderr carry the actual failure.
        }
        finally
        {
            try
            {
                await stdin.DisposeAsync();
            }
            catch (IOException)
            {
                // Same early-exit case as above.
            }
        }
    }

    private static async Task<byte[]> ReadAsync(Stream stream, long maxBytes, bool truncate, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, token);
            if (read == 0)
                break;

            if (buffer.Length + read > maxBytes)
            {
                if (!truncate)
                    throw new ImageMagickException($"ImageMagick output exceeded the limit of {maxBytes} bytes.");

                await buffer.WriteAsync(chunk.AsMemory(0, (int)Math.Max(0, maxBytes - buffer.Length)), token);
                continue;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
        }

        return buffer.ToArray();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill.
        }
    }

    private sealed record ProcessResult(int ExitCode, byte[] Output, string Error);
}
