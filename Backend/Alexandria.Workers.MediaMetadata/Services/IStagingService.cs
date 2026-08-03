namespace Alexandria.Workers.MediaMetadata.Services;

public interface IStagingService
{
    /// <summary>
    /// Downloads the current version of <paramref name="fileId"/> from object storage into the
    /// configured staging directory (<c>{StagedDir}/{fileId}{ext}</c>).
    /// Returns the staged file, or <see langword="null"/> when the file or its current version
    /// does not exist.
    /// </summary>
    Task<StagedFile?> StageAsync(Guid fileId, CancellationToken ct = default);
}

public sealed record StagedFile(Guid FileId, string FilePath, string MimeType);