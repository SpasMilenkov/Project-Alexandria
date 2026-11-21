using System.Xml.Linq;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Which elements of one XML vocabulary carry text and which mark breaks. Text nodes count only
/// inside a container, because Office parts also hold numbers that are not text, such as drawing
/// offsets and sizes.
/// </summary>
internal sealed class XmlTextRules
{
    private static readonly HashSet<XName> None = [];

    public required IReadOnlySet<XName> TextContainers { get; init; }

    /// <summary>Marker elements that stand for a space, like a tab.</summary>
    public IReadOnlySet<XName> Spaces { get; init; } = None;

    /// <summary>Marker elements that stand for a line break.</summary>
    public IReadOnlySet<XName> LineBreaks { get; init; } = None;

    /// <summary>Elements whose end separates words, like a table cell.</summary>
    public IReadOnlySet<XName> SpaceEnds { get; init; } = None;

    /// <summary>Elements whose end closes a line, like a paragraph.</summary>
    public IReadOnlySet<XName> LineEnds { get; init; } = None;

    /// <summary>Elements whose end closes a page, like a slide or a sheet.</summary>
    public IReadOnlySet<XName> PageEnds { get; init; } = None;

    /// <summary>Subtrees left out entirely, like comments or deleted tracked changes.</summary>
    public IReadOnlySet<XName> Skipped { get; init; } = None;
}