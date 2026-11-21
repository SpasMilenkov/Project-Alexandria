namespace Alexandria.Common.Exceptions.Files;

/// <summary>
/// Raised when text content can't be read or written for a version: it's encrypted, too large, or
/// not valid UTF-8 text.
/// </summary>
public sealed class FileContentNotEditableException(Guid id, string reason)
    : Exception(reason)
{
    public Guid Id { get; } = id;
}
