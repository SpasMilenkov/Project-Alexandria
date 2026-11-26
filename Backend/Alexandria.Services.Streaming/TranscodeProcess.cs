using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

/// <summary>
/// Runs ffmpeg for a transcode job while a watchdog polls the job for a cancellation request and
/// records how far the run has come. The watchdog only cancels a token: killing the process is
/// left to <see cref="ExternalProcess"/>, so there is one code path that ends a process however
/// the run is stopped.
/// </summary>
internal static partial class TranscodeProcess
{
    // All ffmpeg runs of one job together, fallbacks included. Below the job recovery stall
    // threshold, which would otherwise fail a job that is still running, and below the broker's
    // consumer timeout, which would hand the job to another worker.
    internal static readonly TimeSpan MaxDuration = TimeSpan.FromHours(5.5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <exception cref="ExternalProcessTimeoutException">
    /// The job's <paramref name="budget"/> ran out, before this run or during it.
    /// </exception>
    public static async Task RunFfmpegAsync(
        TranscodeRequest request,
        TranscodeBudget budget,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken ct)
    {
        var timeLeft = budget.Remaining;

        if (timeLeft <= TimeSpan.Zero)
            throw new ExternalProcessTimeoutException("ffmpeg", budget.Total);

        var ffmpegRequest = request.SourceDuration is null
            ? request
            : request with { Arguments = $"{FfmpegProgress.Arguments} {request.Arguments}" };

        try
        {
            await RunAsync("ffmpeg", ffmpegRequest, timeLeft, PollInterval, unitOfWork, logger, ct);
        }
        catch (ExternalProcessTimeoutException)
        {
            // The run only had what was left; the limit that was broken is the job's.
            throw new ExternalProcessTimeoutException("ffmpeg", budget.Total);
        }
    }

    /// <summary>
    /// Runs <paramref name="tool"/> for the job. With a known source duration its standard output
    /// is read as ffmpeg progress lines.
    /// </summary>
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

        var progress = request.SourceDuration is null ? null : new FfmpegProgress();
        var watchdog = WatchJobAsync(request, progress, runCancel, pollInterval, unitOfWork, logger, watchdogStop.Token);

        ExternalProcessResult result;

        try
        {
            result = progress is null
                ? await ExternalProcess.RunAsync(tool, request.Arguments, maxDuration, logger, runCancel.Token)
                : await ExternalProcess.RunAsync(tool, request.Arguments, maxDuration, progress.Read, logger, runCancel.Token);
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

    private static async Task WatchJobAsync(
        TranscodeRequest request,
        FfmpegProgress? progress,
        CancellationTokenSource runCancel,
        TimeSpan pollInterval,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken stop)
    {
        using var timer = new PeriodicTimer(pollInterval);

        // Each run reports its own progress, so a fallback or a re-encode starts the bar over.
        var reported = -1;

        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                try
                {
                    var status = await unitOfWork.Jobs.GetStatusAsync(request.JobId, stop);

                    if (status == JobStatus.CancellationRequested)
                    {
                        LogJobCancellationRequested(logger, request.JobId);
                        await runCancel.CancelAsync();
                        return;
                    }

                    reported = await ReportProgressAsync(request, progress, reported, unitOfWork, stop);
                }
                catch (Exception ex) when (!stop.IsCancellationRequested)
                {
                    // One failed poll must not end the watchdog, or cancelling the job stops working
                    // for the rest of a transcode that can run for hours.
                    LogWatchdogPollFailed(logger, ex, request.JobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogWatchdogStopped(logger, request.JobId);
        }
    }

    // Writes only when the percentage moved. Each write also refreshes the job's heartbeat.
    private static async Task<int> ReportProgressAsync(
        TranscodeRequest request,
        FfmpegProgress? progress,
        int reported,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (progress is null || request.SourceDuration is not { } duration)
            return reported;

        if (progress.Percent(duration) is not { } percent || percent <= reported)
            return reported;

        await unitOfWork.Jobs.UpdateProgressAsync(request.JobId, percent, ct);

        return percent;
    }
}
