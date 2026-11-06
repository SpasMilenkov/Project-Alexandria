namespace Alexandria.Common.Exceptions.Preview;

/// <summary>
/// Thrown when a preview job reaches a worker after its file version was deleted. Retrying cannot
/// help; if the file is restored, viewing it dispatches the job again.
/// </summary>
public class PreviewSourceDeletedException(Guid versionId)
    : Exception($"Version '{versionId}' has been deleted, so there is nothing to generate a preview from.")
{
    public Guid VersionId { get; } = versionId;
}
