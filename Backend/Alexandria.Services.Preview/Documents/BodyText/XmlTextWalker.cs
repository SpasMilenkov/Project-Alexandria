using System.Xml;
using System.Xml.Linq;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Streams an XML part into a <see cref="TextCollector"/> following a set of
/// <see cref="XmlTextRules"/>, so a large document body never becomes a tree in memory.
/// </summary>
internal static class XmlTextWalker
{
    /// <summary>
    /// Like the property-part settings, but whitespace is kept: Word stores the space between two
    /// differently formatted words as a text run of its own, and dropping it would glue them.
    /// </summary>
    public static XmlReaderSettings Settings { get; } = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = false,
        CloseInput = true
    };

    /// <exception cref="XmlException">The part is not well-formed XML.</exception>
    public static void Read(XmlReader reader, XmlTextRules rules, TextCollector collector)
    {
        var openContainers = 0;

        // Skip() already moves to the next node, which must then be handled without reading on.
        var positioned = false;

        while (!collector.IsFull)
        {
            if (!positioned && !reader.Read())
                break;

            positioned = false;

            if (reader.EOF)
                break;

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    var name = XName.Get(reader.LocalName, reader.NamespaceURI);

                    if (rules.Skipped.Contains(name))
                    {
                        reader.Skip();
                        positioned = true;
                        break;
                    }

                    OnStart(name, rules, collector);

                    if (reader.IsEmptyElement)
                    {
                        OnEnd(name, rules, collector);
                        break;
                    }

                    if (rules.TextContainers.Contains(name))
                        openContainers++;

                    break;
                }

                case XmlNodeType.EndElement:
                {
                    var name = XName.Get(reader.LocalName, reader.NamespaceURI);

                    if (rules.TextContainers.Contains(name) && openContainers > 0)
                        openContainers--;

                    OnEnd(name, rules, collector);
                    break;
                }

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    if (openContainers > 0)
                        collector.Append(reader.Value, inline: true);
                    break;
            }
        }
    }

    private static void OnStart(XName name, XmlTextRules rules, TextCollector collector)
    {
        if (rules.Spaces.Contains(name))
            collector.Space();

        if (rules.LineBreaks.Contains(name))
            collector.LineBreak();
    }

    private static void OnEnd(XName name, XmlTextRules rules, TextCollector collector)
    {
        if (rules.SpaceEnds.Contains(name))
            collector.Space();

        if (rules.LineEnds.Contains(name))
            collector.LineBreak();

        if (rules.PageEnds.Contains(name))
            collector.PageBreak();
    }
}