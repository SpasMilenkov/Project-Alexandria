using Alexandria.Common.Mime;

namespace Alexandria.Services.Storage.Content.Mime;

/// <inheritdoc />
public sealed class MimeTypeResolver(IMimeSniffer sniffer) : IMimeTypeResolver
{
    private const string TextPlain = "text/plain";
    private const string ApplicationZip = "application/zip";

    /// <summary>
    /// Types that say little about a file. A match here never overrides a known extension, except
    /// that a zip signature beats a name that is not zip-based (rule 2a).
    /// </summary>
    private static readonly HashSet<string> GenericTypes = new(StringComparer.Ordinal)
    {
        ApplicationZip,
        "application/xml",
        TextPlain,
        MimeSniffing.Octet
    };

    /// <inheritdoc />
    public MimeResolution Resolve(string fileName, byte[] header, string? declared = null)
    {
        header ??= [];

        var declaredType = NormalizeDeclared(declared);
        var sniffed = header.Length == 0 ? null : NormalizeSniffed(sniffer.Detect(header));
        var extensionType = ExtensionMimeMap.Lookup(fileName, header);
        var fileNameType = MimeFileNameMap.Lookup(fileName, header);

        // Rule 0: a well-known filename that has no reliable extension, or one whose name is more
        // specific than its generic extension. Only trust it when the bytes are text (or empty),
        // so a renamed binary still goes through normal sniffing.
        if (fileNameType is not null
            && (extensionType is null || MimeFileNameMap.OverridesExtension(fileName))
            && (header.Length == 0 || MimeTextHeuristic.LooksLikeText(header)))
            return new MimeResolution(fileNameType, MimeSource.FileName, sniffed);

        // Rule 1: a specific sniffed type
        if (sniffed is not null && !GenericTypes.Contains(sniffed))
        {
            // Rule 1-text: text signatures (message/rfc822, shell scripts and the like) are guesses.
            // When the bytes are plainly text and the extension names a text format, the extension wins.
            if (extensionType is not null && IsTextualType(extensionType) && MimeTextHeuristic.LooksLikeText(header))
                return new MimeResolution(extensionType, MimeSource.Extension, sniffed);

            if (extensionType is not null && MimeFamilies.SameFamily(extensionType, sniffed))
                return new MimeResolution(extensionType, MimeSource.Extension, sniffed);

            return new MimeResolution(sniffed, MimeSource.SniffedSpecific, sniffed);
        }

        // Rule 2: the extension
        if (extensionType is not null)
        {
            if (sniffed == ApplicationZip && !MimeFamilies.IsZipBased(extensionType))
                return new MimeResolution(ApplicationZip, MimeSource.SniffedGeneric, sniffed);

            return new MimeResolution(extensionType, MimeSource.Extension, sniffed);
        }

        // Rule 3: a generic sniffed type, as long as it says something
        if (sniffed is not null && sniffed != MimeSniffing.Octet)
            return new MimeResolution(sniffed, MimeSource.SniffedGeneric, sniffed);

        // Rule 4: the declared type
        if (declaredType is not null)
            return new MimeResolution(declaredType, MimeSource.Declared, sniffed);

        // Rule 5: text heuristic
        if (MimeTextHeuristic.LooksLikeText(header))
            return new MimeResolution(TextPlain, MimeSource.TextHeuristic, sniffed);

        // Rule 6
        return new MimeResolution(MimeSniffing.Octet, MimeSource.Fallback, sniffed);
    }

    /// <inheritdoc />
    public MimeResolution ResolveWithoutContent(string fileName, string? declared)
    {
        var fileNameType = MimeFileNameMap.Lookup(fileName);
        var extensionType = ExtensionMimeMap.Lookup(fileName, header: null);

        if (fileNameType is not null
            && (extensionType is null || MimeFileNameMap.OverridesExtension(fileName)))
            return new MimeResolution(fileNameType, MimeSource.FileName, null);

        var declaredType = NormalizeDeclared(declared);
        if (declaredType is not null)
            return new MimeResolution(declaredType, MimeSource.Declared, null);

        if (extensionType is not null)
            return new MimeResolution(extensionType, MimeSource.Extension, null);

        return new MimeResolution(MimeSniffing.Octet, MimeSource.Fallback, null);
    }

    /// <summary>Canonical declared type, or null when it is empty, malformed or octet-stream.</summary>
    private static string? NormalizeDeclared(string? declared)
    {
        var canonical = MimeAliases.Canonicalize(declared);
        return MimeAliases.IsUsable(canonical) ? canonical : null;
    }

    /// <summary>Canonical sniffed type, or null when the sniffer returned nothing well-formed.</summary>
    private static string? NormalizeSniffed(string? sniffed)
    {
        var canonical = MimeAliases.Canonicalize(sniffed);
        return MimeAliases.HasValidShape(canonical) ? canonical : null;
    }
    
    private static bool IsTextualType(string canonical)
        => MimeCategories.IsText(canonical) || canonical == "image/svg+xml";
}
