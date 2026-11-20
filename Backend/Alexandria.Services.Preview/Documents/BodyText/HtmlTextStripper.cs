using System.Net;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Turns (X)HTML into plain text without a parser. EPUB chapters are meant to be XHTML, but many
/// use HTML entities like <c>&amp;nbsp;</c> that a strict XML reader rejects, so a forgiving
/// scan reads more books. Block elements end a line; everything else is dropped.
/// </summary>
internal static class HtmlTextStripper
{
    // The head holds the chapter's title again and its styles; scripts and styles are never text.
    private static readonly HashSet<string> SkippedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "script", "style", "template", "svg", "math"
    };

    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "body", "br", "dd", "div", "dl", "dt",
        "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr",
        "html", "li", "main", "nav", "ol", "p", "pre", "section", "table", "tr", "ul"
    };

    private static readonly HashSet<string> CellElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "td", "th"
    };

    public static void Strip(string html, TextCollector collector)
    {
        var position = 0;

        while (position < html.Length && !collector.IsFull)
        {
            var tagStart = html.IndexOf('<', position);
            var textEnd = tagStart < 0 ? html.Length : tagStart;

            AppendDecoded(html.AsSpan(position, textEnd - position), collector);

            if (tagStart < 0)
                return;

            position = SkipMarkup(html, tagStart, collector);
        }
    }

    // Returns where text resumes after the markup starting at the '<'.
    private static int SkipMarkup(string html, int tagStart, TextCollector collector)
    {
        var rest = html.AsSpan(tagStart);

        if (rest.StartsWith("<!--"))
            return After(html, "-->", tagStart + 4);

        if (rest.StartsWith("<![CDATA["))
        {
            var dataStart = tagStart + 9;
            var dataEnd = html.IndexOf("]]>", dataStart, StringComparison.Ordinal);
            var end = dataEnd < 0 ? html.Length : dataEnd;

            collector.Append(html.AsSpan(dataStart, end - dataStart), inline: true);
            return dataEnd < 0 ? html.Length : dataEnd + 3;
        }

        var tagEnd = TagEnd(html, tagStart + 1);
        if (tagEnd < 0)
            return html.Length;

        var tag = html.AsSpan(tagStart + 1, tagEnd - tagStart - 1);
        var isClosing = tag.StartsWith("/");
        var name = TagName(isClosing ? tag[1..] : tag);
        var isSelfClosing = tag.EndsWith("/");

        if (!isClosing && !isSelfClosing && SkippedElements.Contains(name))
            return After(html, $"</{name}", tagEnd + 1, closeTag: true);

        if (BlockElements.Contains(name))
            collector.LineBreak();
        else if (CellElements.Contains(name))
            collector.Space();

        return tagEnd + 1;
    }

    // Finds the '>' that ends a tag, ignoring any inside quoted attribute values.
    private static int TagEnd(string html, int from)
    {
        var quote = '\0';

        for (var i = from; i < html.Length; i++)
        {
            var c = html[i];

            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '>')
                return i;
        }

        return -1;
    }

    private static string TagName(ReadOnlySpan<char> tag)
    {
        var end = 0;

        while (end < tag.Length && (char.IsLetterOrDigit(tag[end]) || tag[end] is ':' or '-'))
            end++;

        var name = tag[..end];
        var prefix = name.IndexOf(':');

        // Namespaced XHTML (html:p) is the same element as p.
        return (prefix >= 0 ? name[(prefix + 1)..] : name).ToString();
    }

    private static int After(string html, string marker, int from, bool closeTag = false)
    {
        var found = html.IndexOf(marker, from, StringComparison.OrdinalIgnoreCase);
        if (found < 0)
            return html.Length;

        if (!closeTag)
            return found + marker.Length;

        var tagEnd = html.IndexOf('>', found);
        return tagEnd < 0 ? html.Length : tagEnd + 1;
    }

    private static void AppendDecoded(ReadOnlySpan<char> text, TextCollector collector)
    {
        if (text.IsEmpty)
            return;

        if (text.Contains('&'))
        {
            collector.Append(WebUtility.HtmlDecode(text.ToString()), inline: true);
            return;
        }

        collector.Append(text, inline: true);
    }
}