using Microsoft.Extensions.Logging;

namespace Alexandria.Common.Processes;

public static partial class ExternalProcess
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cancelled {FileName} (pid {ProcessId}), killing it.")]
    private static partial void LogCancelled(ILogger logger, string fileName, int processId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{FileName} (pid {ProcessId}) exceeded its {Timeout} time limit, killing it.")]
    private static partial void LogTimedOut(ILogger logger, string fileName, int processId, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not kill {FileName} (pid {ProcessId}) or part of what it started.")]
    private static partial void LogKillFailed(ILogger logger, Exception ex, string fileName, int processId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{FileName} (pid {ProcessId}) was still running {Wait} after being killed, moving on without it.")]
    private static partial void LogStillRunningAfterKill(ILogger logger, string fileName, int processId, TimeSpan wait);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stopped reading {Stream} of {FileName} after {Grace}: another process is still holding it open.")]
    private static partial void LogDrainStopped(ILogger logger, string stream, string fileName, TimeSpan grace);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Reading {Stream} of {FileName} failed, keeping what was read so far.")]
    private static partial void LogDrainFailed(ILogger logger, Exception ex, string stream, string fileName);
}
