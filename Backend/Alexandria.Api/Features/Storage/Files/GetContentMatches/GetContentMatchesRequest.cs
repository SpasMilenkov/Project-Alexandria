using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetContentMatches;

internal sealed class GetContentMatchesRequest
{
    public Guid FileId { get; set; }

    /// <summary>The search text, parsed the way the file search parses it.</summary>
    [QueryParam]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Only the excerpt at the first match, which is much cheaper on long documents. The search
    /// rows ask for this; the preview pane asks for every excerpt.
    /// </summary>
    [QueryParam]
    public bool FirstMatchOnly { get; set; }
}