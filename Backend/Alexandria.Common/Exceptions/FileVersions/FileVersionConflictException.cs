namespace Alexandria.Common.Exceptions.FileVersions;

/// <summary>
/// Raised when a save names a parent version that is no longer the file's current version, for
/// example after a save from another tab or a version restore. Nothing is written.
/// </summary>
public sealed class FileVersionConflictException(Guid fileId, Guid parentVersionId, Guid? currentVersionId)
    : Exception($"File {fileId} moved on from version {parentVersionId} to {currentVersionId}.")
{
    public Guid FileId { get; } = fileId;
    public Guid ParentVersionId { get; } = parentVersionId;
    public Guid? CurrentVersionId { get; } = currentVersionId;
}
