namespace Alexandria.Workers.MediaMetadata.Services;

/// <summary>
/// Staging layout. Each trigger downloads into its own file under <c>{StagedDir}/incoming</c>,
/// and a dispatched batch owns <c>{StagedDir}/{batchId}</c>. Concurrent batches for the same
/// file therefore never overwrite, read or clean up each other's inputs.
/// </summary>
public static class StagingPaths
{
    private const string IncomingFolder = "incoming";

    public static string IncomingDirectory(string stagedDir) => Path.Combine(stagedDir, IncomingFolder);

    public static string NewIncomingPath(string stagedDir, Guid fileId, string mimeType)
    {
        var extension = MimeTypeExtensions.GetExtension(mimeType);
        return Path.Combine(IncomingDirectory(stagedDir), $"{fileId:N}-{Guid.NewGuid():N}{extension}");
    }

    public static string BatchDirectory(string stagedDir, Guid batchId) => Path.Combine(stagedDir, batchId.ToString());

    /// <remarks>
    /// The classifier names each output after its input's file name, and the results consumer
    /// reads <c>{fileId}.json</c>, so the input keeps the file ID as its stem.
    /// </remarks>
    public static string BatchInputPath(string stagedDir, Guid batchId, StagedFile file)
    {
        var extension = MimeTypeExtensions.GetExtension(file.MimeType);
        return Path.Combine(BatchDirectory(stagedDir, batchId), $"{file.FileId}{extension}");
    }

    /// <summary>
    /// Deletes a staged file without throwing: cleanup must never fail the operation that owns it.
    /// </summary>
    public static bool TryDeleteFile(string path, out Exception? error)
    {
        error = null;

        try
        {
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    /// <summary>
    /// Recursively deletes a staged directory without throwing.
    /// </summary>
    public static bool TryDeleteDirectory(string path, out Exception? error)
    {
        error = null;

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }
}
