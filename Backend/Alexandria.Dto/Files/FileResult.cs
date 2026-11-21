using Alexandria.Dto.Tags;

namespace Alexandria.Dto.Files;

public record FileResult(
    Guid FileId,
    string FileName,
    string MimeType,
    Guid? DirectoryId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    FileVersionDto CurrentVersion,
    List<TagDto> Tags,
    UserDto Owner)
{
    /// <summary>
    /// True in search results when the query's words were found in the document's text. Always
    /// false outside a text search.
    /// </summary>
    public bool MatchedInContent { get; init; }
}