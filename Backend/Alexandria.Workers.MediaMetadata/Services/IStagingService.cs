namespace Alexandria.Workers.MediaMetadata.Services;

public interface IStagingService
{
    /// <summary>
    /// Downloads the current version of <paramref name="fileId"/> from object storage into a
    /// staging path unique to this call (see <see cref="StagingPaths.NewIncomingPath"/>).
    /// Returns the staged file, or <see langword="null"/> when the file or its current version
    /// does not exist.
    /// </summary>
    Task<StagedFile?> StageAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Removes a staged file that will not be handed to a batch. Never throws.
    /// </summary>
    void Discard(StagedFile file);
}

public sealed record StagedFile(Guid FileId, string FilePath, string MimeType);
