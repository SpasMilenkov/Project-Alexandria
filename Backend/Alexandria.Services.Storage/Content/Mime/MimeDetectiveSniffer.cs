using Alexandria.Common.Mime;
using Microsoft.Extensions.Logging;
using MimeDetective;
using MimeDetective.Definitions;

namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// <see cref="IMimeSniffer"/> backed by MimeDetective. The content inspector is built once in the
/// constructor because building is expensive. The built-in default definitions are used because
/// the optional Condensed and Exhaustive packs carry the upstream TrID non-commercial terms.
/// Those defaults cannot tell ISO BMFF brands apart, so <see cref="IsoBmffBrands"/> is consulted first.
/// This is the only catch in the MIME path: any failure is logged at debug level (never with file
/// content) and reported as "no match", so sniffing can never reject an upload.
/// </summary>
public sealed partial class MimeDetectiveSniffer : IMimeSniffer
{
    private const string AmbiguousCompressed = "application/x-compressed";
    private const string SevenZip = "application/x-7z-compressed";
    private const string Zip = "application/zip";

    private readonly ILogger<MimeDetectiveSniffer> logger;
    private readonly Func<byte[], string?> inspect;

    public MimeDetectiveSniffer(ILogger<MimeDetectiveSniffer> logger)
        : this(logger, CreateInspector())
    {
    }

    /// <summary>Test seam: lets tests inject a delegate that returns a type or throws.</summary>
    internal MimeDetectiveSniffer(ILogger<MimeDetectiveSniffer> logger, Func<byte[], string?> inspect)
    {
        this.logger = logger;
        this.inspect = inspect;
    }

    /// <inheritdoc />
    public string? Detect(byte[] header)
    {
        if (header is null || header.Length == 0) return null;

        try
        {
            return IsoBmffBrands.Detect(header) ?? inspect(header);
        }
        catch (Exception ex)
        {
            LogSniffFailed(logger, ex);
            return null;
        }
    }

    private static Func<byte[], string?> CreateInspector()
    {
        var inspector = new ContentInspectorBuilder
        {
            Definitions = DefaultDefinitions.All()
        }.Build();

        return header =>
        {
            string? best = null;
            var bestPoints = long.MinValue;

            foreach (var match in inspector.Inspect(header))
            {
                var file = match.Definition.File;
                if (file is null || string.IsNullOrWhiteSpace(file.MimeType)) continue;
                if (match.Points <= bestPoints) continue;

                best = ResolveMimeType(file.MimeType, file.Extensions);
                bestPoints = match.Points;
            }

            return best;
        };
    }

    /// <summary>
    /// MimeDetective reports zip and 7z as <c>application/x-compressed</c>. The definition's
    /// extension list is the only reliable way to tell them apart, so the ambiguous type is
    /// mapped here instead of leaking into the resolver.
    /// </summary>
    private static string ResolveMimeType(string mimeType, IEnumerable<string> extensions)
    {
        var canonical = MimeAliases.Canonicalize(mimeType);
        if (canonical != AmbiguousCompressed) return canonical;

        foreach (var extension in extensions)
        {
            if (string.Equals(extension, "7z", StringComparison.OrdinalIgnoreCase)) return SevenZip;
            if (string.Equals(extension, "zip", StringComparison.OrdinalIgnoreCase)) return Zip;
        }

        return canonical;
    }

    [LoggerMessage(1060, LogLevel.Debug, "MIME sniffing failed, falling back to name and declared type")]
    private static partial void LogSniffFailed(ILogger logger, Exception ex);
}
