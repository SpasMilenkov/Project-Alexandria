using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Alexandria.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Alexandria.Common.Processes;

/// <summary>
/// Runs an external tool so that it can neither block on a full output pipe nor outlive the
/// call: both streams are read from the moment it starts, and a tool that has not exited when
/// the call ends is killed along with anything it started.
/// </summary>
public static partial class ExternalProcess
{
    internal const int StdErrTailChars = 8 * 1024;
    internal const int MaxStdOutChars = 16 * 1024 * 1024;

    private const int ChunkChars = 8 * 1024;

    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the tool to completion and returns its exit code and output. A non-zero exit code is
    /// a result, not an exception, because some callers treat it as a warning.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    /// <exception cref="ExternalProcessTimeoutException">The tool outran <paramref name="timeout"/>.</exception>
    /// <exception cref="Win32Exception">The tool could not be started.</exception>
    public static async Task<ExternalProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken ct)
    {
        // Fallback paths can run after cancellation, and anything started there would keep
        // running with nobody waiting for it.
        ct.ThrowIfCancellationRequested();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        // ffmpeg reads stdin for hotkeys and overwrite prompts, so it must see EOF instead of waiting.
        process.StandardInput.Close();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        using var drainStop = new CancellationTokenSource();
        var stdout = DrainAsync(process.StandardOutput, AppendHead, fileName, "stdout", logger, drainStop.Token);
        var stderr = DrainAsync(process.StandardError, AppendTail, fileName, "stderr", logger, drainStop.Token);

        var exited = false;

        try
        {
            await process.WaitForExitAsync(deadline.Token);
            exited = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogCancelled(logger, fileName, process.Id);
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimedOut(logger, fileName, process.Id, timeout);
            throw new ExternalProcessTimeoutException(fileName, timeout);
        }
        finally
        {
            if (!exited)
                await KillAndWaitAsync(process, fileName, logger);

            // A tool can leave a background process behind that keeps the pipes open after the
            // tool itself has exited, so reading has to stop after a while instead of waiting
            // for the end of the stream.
            drainStop.CancelAfter(DrainGrace);
            await Task.WhenAll(stdout, stderr);
        }

        return new ExternalProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static async Task KillAndWaitAsync(Process process, string fileName, ILogger logger)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AggregateException or Win32Exception)
        {
            LogKillFailed(logger, ex, fileName, process.Id);
        }

        try
        {
            using var wait = new CancellationTokenSource(KillWait);
            await process.WaitForExitAsync(wait.Token);
        }
        catch (OperationCanceledException)
        {
            LogStillRunningAfterKill(logger, fileName, process.Id, KillWait);
        }
    }

    private static async Task<string> DrainAsync(
        StreamReader reader,
        Action<StringBuilder, char[], int> append,
        string fileName,
        string stream,
        ILogger logger,
        CancellationToken stop)
    {
        var captured = new StringBuilder();
        var chunk = new char[ChunkChars];

        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(chunk.AsMemory(), stop);
                if (read == 0)
                    break;

                append(captured, chunk, read);
            }
        }
        catch (OperationCanceledException)
        {
            LogDrainStopped(logger, stream, fileName, DrainGrace);
        }
        catch (IOException ex)
        {
            LogDrainFailed(logger, ex, stream, fileName);
        }

        return captured.ToString();
    }

    // Past the cap the chunks are still read and discarded, otherwise the pipe would fill up.
    private static void AppendHead(StringBuilder captured, char[] chunk, int count) =>
        captured.Append(chunk, 0, Math.Min(count, MaxStdOutChars - captured.Length));

    // ffmpeg writes its banner first and the actual error last, so the end is the part worth keeping.
    private static void AppendTail(StringBuilder captured, char[] chunk, int count)
    {
        captured.Append(chunk, 0, count);

        if (captured.Length > StdErrTailChars)
            captured.Remove(0, captured.Length - StdErrTailChars);
    }
}
