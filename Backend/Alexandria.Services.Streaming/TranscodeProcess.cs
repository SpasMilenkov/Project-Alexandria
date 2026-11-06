using Alexandria.Common;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Runs ffmpeg for a transcode job while a watchdog polls the job for a cancellation request.
/// The watchdog only cancels a token: killing the process is left to <see cref="ExternalProcess"/>,
/// so there is one code path that ends a process however the run is stopped.
/// </summary>
internal static partial class TranscodeProcess
{
    // Below the job recovery stall threshold, which would otherwise fail a job that is still running.
    internal static readonly TimeSpan MaxDuration = TimeSpan.FromHours(5.5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public static Task RunFfmpegAsync(
        TranscodeRequest request,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken ct) =>
        RunAsync("ffmpeg", request, MaxDuration, PollInterval, unitOfWork, logger, ct);

    internal static async Task RunAsync(
        string tool,
        TranscodeRequest request,
        TimeSpan maxDuration,
        TimeSpan pollInterval,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken ct)
    {
        using var runCancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var watchdogStop = new CancellationTokenSource();

        var watchdog = WatchForJobCancellationAsync(
            request.JobId, runCancel, pollInterval, unitOfWork, logger, watchdogStop.Token);

        ExternalProcessResult result;

        try
        {
            result = await ExternalProcess.RunAsync(tool, request.Arguments, maxDuration, logger, runCancel.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TranspilationCancelledException(request.JobId);
        }
        finally
        {
            // The watchdog shares the caller's scoped DbContext, which allows no concurrent use, so
            // it has to be finished before the caller gets control back.
            await watchdogStop.CancelAsync();
            await watchdog;
        }

        if (result.ExitCode != 0)
        {
            throw new TranspilationFfmpegException(
                request.InputPath, request.Operation, result.ExitCode, result.StdErrTail);
        }

        if (!File.Exists(request.ExpectedOutput))
            throw new TranspilationFfmpegException(request.InputPath, request.Operation, request.ExpectedOutput);
    }

    private static async Task WatchForJobCancellationAsync(
        Guid jobId,
        CancellationTokenSource runCancel,
        TimeSpan pollInterval,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken stop)
    {
        using var timer = new PeriodicTimer(pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                try
                {
                    var status = await unitOfWork.Jobs.GetStatusAsync(jobId, stop);

                    if (status == JobStatus.CancellationRequested)
                    {
                        LogJobCancellationRequested(logger, jobId);
                        await runCancel.CancelAsync();
                        return;
                    }
                }
                catch (Exception ex) when (!stop.IsCancellationRequested)
                {
                    // One failed poll must not end the watchdog, or cancelling the job stops working
                    // for the rest of a transcode that can run for hours.
                    LogWatchdogPollFailed(logger, ex, jobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogWatchdogStopped(logger, jobId);
        }
    }
}
