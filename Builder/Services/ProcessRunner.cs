using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Builder.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut = false)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

public interface IProcessRunner
{
    ProcessResult Run(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onStandardOutput = null,
        Action<string>? onStandardError = null,
        bool captureOutput = true);
}

public sealed class ProcessRunner : IProcessRunner
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public ProcessRunner() : this(Process.Start)
    {
    }

    internal ProcessRunner(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess;
    }

    public ProcessResult Run(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onStandardOutput = null,
        Action<string>? onStandardError = null,
        bool captureOutput = true)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The process deadline must be positive.");
        }

        using var process = _startProcess(startInfo)
            ?? throw new InvalidOperationException($"{startInfo.FileName} process could not be started");

        return Drain(process, timeout, cancellationToken, onStandardOutput, onStandardError, captureOutput);
    }

    internal static ProcessResult Drain(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onStandardOutput = null,
        Action<string>? onStandardError = null,
        bool captureOutput = true)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using var pumpFailure = new CancellationTokenSource();
        using var pumpStop = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token, pumpFailure.Token);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var callbackGate = new object();
        ExceptionDispatchInfo? failure = null;

        async Task Pump(StreamReader reader, StringBuilder captured, Action<string>? callback)
        {
            try
            {
                while (await reader.ReadLineAsync(pumpStop.Token).ConfigureAwait(false) is { } line)
                {
                    if (captureOutput)
                    {
                        captured.AppendLine(line);
                    }

                    if (callback is not null)
                    {
                        lock (callbackGate)
                        {
                            if (failure is null)
                            {
                                callback(line);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (pumpStop.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lock (callbackGate)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }

                pumpFailure.Cancel();
            }
        }

        var outputPump = Task.Run(() => Pump(process.StandardOutput, stdout, onStandardOutput));
        var errorPump = Task.Run(() => Pump(process.StandardError, stderr, onStandardError));
        var pumps = Task.WhenAll(outputPump, errorPump);
        var completed = Task.WhenAll(pumps, process.WaitForExitAsync());

        try
        {
            completed.WaitAsync(linked.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            var terminationFailure = Terminate(process);
            var drainFailure = DrainAfterTermination(pumps, pumpStop);

            failure?.Throw();
            cancellationToken.ThrowIfCancellationRequested();

            if (terminationFailure is not null)
            {
                ExceptionDispatchInfo.Capture(terminationFailure).Throw();
            }

            if (drainFailure is not null)
            {
                ExceptionDispatchInfo.Capture(drainFailure).Throw();
            }

            if (!deadline.IsCancellationRequested)
            {
                throw;
            }

            return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
        }
        catch
        {
            Terminate(process);
            DrainAfterTermination(pumps, pumpStop);
            throw;
        }

        failure?.Throw();
        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static Exception? DrainAfterTermination(Task pumps, CancellationTokenSource pumpStop)
    {
        try
        {
            pumps.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            // A descendant can retain a pipe after the command has exited.
            pumpStop.Cancel();

            try
            {
                pumps.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        return null;
    }

    private static Exception? Terminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (!process.WaitForExit(2000))
            {
                return new TimeoutException("The child process did not terminate after its process tree was stopped.");
            }
        }
        catch (InvalidOperationException)
        {
            // The child can exit while its termination is being requested.
        }
        catch (Exception exception)
        {
            return exception;
        }

        return null;
    }
}
