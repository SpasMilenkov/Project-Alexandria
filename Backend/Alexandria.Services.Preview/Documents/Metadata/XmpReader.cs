using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Flattens an XMP packet into <c>xmp:prefix:name</c> fields. Newer PDFs keep their properties
/// here instead of in the info dictionary, and it is the only place a PDF names its language.
/// </summary>
internal static partial class XmpReader
{
    /// <summary>
    /// Packets are padded to a few kilobytes; one this size is embedding something else.
    /// </summary>
    internal const int MaxPacketChars = 1024 * 1024;

    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    // Prefixes are chosen by each writer, so known namespaces get fixed ones and keys stay stable.
    private static readonly Dictionary<string, string> KnownPrefixes = new(StringComparer.Ordinal)
    {
        ["http://purl.org/dc/elements/1.1/"] = "dc",
        ["http://ns.adobe.com/xap/1.0/"] = "xmp",
        ["http://ns.adobe.com/pdf/1.3/"] = "pdf",
        ["http://ns.adobe.com/xap/1.0/mm/"] = "xmpMM",
        ["http://www.aiim.org/pdfa/ns/id/"] = "pdfaid",
        ["http://ns.adobe.com/photoshop/1.0/"] = "photoshop"
    };

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        CloseInput = true
    };

    public static void ReadInto(string packet, DocumentFieldBag fields, ILogger logger)
    {
        if (packet.Length > MaxPacketChars)
        {
            LogPacketTooLarge(logger, packet.Length);
            return;
        }

        XDocument document;

        try
        {
            using var reader = XmlReader.Create(new StringReader(packet.Trim().TrimStart('﻿')), Settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            // An encrypted PDF hands back its packet still encrypted, which is not XML.
            LogPacketUnreadable(logger, ex);
            return;
        }

        var descriptions = document.Descendants(XName.Get("Description", RdfNamespace));

        foreach (var description in descriptions)
        {
            foreach (var attribute in description.Attributes())
            {
                if (attribute.IsNamespaceDeclaration || attribute.Name.NamespaceName is "" or RdfNamespace)
                    continue;

                fields.Add(Key(attribute.Name, description), attribute.Value);
            }

            foreach (var property in description.Elements())
                fields.Add(Key(property.Name, property), ValueOf(property));
        }
    }

    // Lists (rdf:Seq, rdf:Bag) and language alternatives (rdf:Alt) keep their values in rdf:li.
    private static string ValueOf(XElement property)
    {
        var items = property.Descendants(XName.Get("li", RdfNamespace))
            .Select(li => li.Value.Trim())
            .Where(value => value.Length > 0)
            .ToList();

        return items.Count > 0 ? string.Join("; ", items) : property.Value;
    }

    private static string Key(XName name, XElement scope)
    {
        var prefix = KnownPrefixes.GetValueOrDefault(name.NamespaceName)
                     ?? scope.GetPrefixOfNamespace(name.Namespace)
                     ?? "ns";

        return $"xmp:{prefix}:{name.LocalName}";
    }

    [LoggerMessage(4272, LogLevel.Debug, "Skipped an XMP packet of {Length} characters")]
    private static partial void LogPacketTooLarge(ILogger logger, int length);

    [LoggerMessage(4273, LogLevel.Debug, "Skipped an XMP packet that is not readable XML")]
    private static partial void LogPacketUnreadable(ILogger logger, Exception ex);
}
