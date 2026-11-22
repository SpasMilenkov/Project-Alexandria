namespace Alexandria.Common.Exceptions.Files;

/// <summary>
/// Raised when a write names a draft revision other than the stored one, which means another tab
/// or device wrote the draft since this editor last saw it. Nothing is written.
/// </summary>
public sealed class FileDraftConflictException(Guid fileId, Guid? expectedRevision, Guid storedRevision)
    : Exception($"The draft of file {fileId} is at revision {storedRevision}, not {expectedRevision}.")
{
    public Guid FileId { get; } = fileId;
    public Guid? ExpectedRevision { get; } = expectedRevision;
    public Guid StoredRevision { get; } = storedRevision;
}
