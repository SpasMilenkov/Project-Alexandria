using Microsoft.Extensions.Logging;

namespace Alexandria.Common.Processes;

/// <summary>
/// Gives each job its own folder, so everything the job and its tools write goes away with the
/// folder, including partial outputs and files whose names only the tool knows.
/// </summary>
public static partial class JobScratch
{
    private const string FolderPrefix = "job-";

    public static string Create() => Create(Path.GetTempPath());

    internal static string Create(string root) =>
        Directory.CreateDirectory(Path.Combine(root, $"{FolderPrefix}{Guid.NewGuid()}")).FullName;

    /// <summary>
    /// Deletes the folder. A failure is logged instead of thrown because this runs in a finally
    /// block, where throwing would replace the exception that says why the job failed.
    /// </summary>
    public static void Delete(string folder, ILogger logger)
    {
        if (!Directory.Exists(folder))
            return;

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, ex, folder);
        }
    }

    /// <summary>
    /// Removes folders from runs that died before reaching their cleanup. Call this before the
    /// worker starts taking jobs, and only where one worker process uses the temp directory:
    /// the folder of a job running in another process looks exactly like a leftover.
    /// </summary>
    public static void SweepLeftovers(ILogger logger) => SweepLeftovers(Path.GetTempPath(), logger);

    internal static void SweepLeftovers(string root, ILogger logger)
    {
        List<string> leftovers;

        try
        {
            leftovers = Directory.EnumerateDirectories(root, $"{FolderPrefix}*").ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSweepFailed(logger, ex, root);
            return;
        }

        foreach (var folder in leftovers)
            Delete(folder, logger);

        if (leftovers.Count > 0)
            LogSwept(logger, leftovers.Count, root);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not delete job folder {Folder}, the next worker start tries again.")]
    private static partial void LogDeleteFailed(ILogger logger, Exception ex, string folder);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not look for leftover job folders in {Root}.")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex, string root);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Found {Count} job folders left behind by an earlier run in {Root}.")]
    private static partial void LogSwept(ILogger logger, int count, string root);
}
