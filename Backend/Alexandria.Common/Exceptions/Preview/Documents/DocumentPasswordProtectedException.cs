namespace Alexandria.Common.Exceptions.Preview.Documents;

/// <summary>
/// Thrown when a document needs a password to open, so no preview can be rendered from it.
/// Retrying cannot help until a new version without the password is uploaded.
/// </summary>
public class DocumentPasswordProtectedException(Guid versionId)
    : Exception($"Version '{versionId}' is password protected, so no preview can be rendered from it.")
{
    public Guid VersionId { get; } = versionId;
}
