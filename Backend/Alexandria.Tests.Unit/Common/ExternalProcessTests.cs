using System.ComponentModel;
using System.Diagnostics;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Processes;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Common;

public class ExternalProcessTests
{
    private const int OneMiB = 1024 * 1024;

    // Long enough to catch a regression as a failure, since the bug under test is a hang.
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(30);

    private static void RequireLinux() =>
        Assert.SkipUnless(OperatingSystem.IsLinux(), "These tests spawn sh and inspect /proc.");

    private static Task<ExternalProcessResult> RunShellAsync(string script, TimeSpan timeout, CancellationToken ct) =>
        RunShellAsync(script, timeout, NullLogger.Instance, ct);

    private static Task<ExternalProcessResult> RunShellAsync(
        string script,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken ct) =>
        ExternalProcess.RunAsync("sh", $"-c \"{script}\"", timeout, logger, ct);

    [Fact]
    public async Task RunAsync_withEnvironment_toolSeesTheValue()
    {
        RequireLinux();

        var result = await ExternalProcess.RunAsync(
            "sh",
            "-c \"printenv TMPDIR\"",
            GenerousTimeout,
            new Dictionary<string, string> { ["TMPDIR"] = "/job/folder" },
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        result.StdOut.TrimEnd().Should().Be("/job/folder");
    }

    [Fact]
    public async Task RunAsync_stderrLargerThanPipe_returnsExitCodeAndFinalLine()
    {
        RequireLinux();

        var result = await RunShellAsync(
            $"yes e | head -c {OneMiB} >&2; echo END-MARK >&2; exit 3",
            GenerousTimeout,
            TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(3);
        result.StdErrTail.TrimEnd().Should().EndWith("END-MARK");
    }

    [Fact]
    public async Task RunAsync_bothStreamsLargerThanPipe_returnsAllOfStdOut()
    {
        RequireLinux();

        var result = await RunShellAsync(
            $"yes e | head -c {OneMiB} >&2; yes o | head -c {OneMiB}",
            GenerousTimeout,
            TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().HaveLength(OneMiB);
    }

    [Fact]
    public async Task RunAsync_cancelled_killsProcessTreeLogsAndThrows()
    {
        RequireLinux();

        var duration = SleepProbe.UniqueSleepDuration(60);
        var logger = new RecordingLogger<ExternalProcessTests>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        var act = () => RunShellAsync($"sleep {duration} & sleep {duration}; wait", GenerousTimeout, logger, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await SleepProbe.SleepStopsWithinAsync(duration, TimeSpan.FromSeconds(2))).Should().BeTrue();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("Cancelled sh"));
    }

    [Fact]
    public async Task RunAsync_timedOut_killsProcessTreeLogsAndThrowsTimeout()
    {
        RequireLinux();

        var duration = SleepProbe.UniqueSleepDuration(60);
        var logger = new RecordingLogger<ExternalProcessTests>();

        var act = () => RunShellAsync(
            $"sleep {duration} & sleep {duration}; wait",
            TimeSpan.FromMilliseconds(300),
            logger,
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ExternalProcessTimeoutException>()).Which.FileName.Should().Be("sh");
        (await SleepProbe.SleepStopsWithinAsync(duration, TimeSpan.FromSeconds(2))).Should().BeTrue();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("time limit"));
    }

    [Fact]
    public async Task RunAsync_backgroundProcessHoldsPipesAfterExit_returnsAfterGracePeriodAndLogs()
    {
        RequireLinux();

        var logger = new RecordingLogger<ExternalProcessTests>();
        var watch = Stopwatch.StartNew();

        var result = await RunShellAsync(
            $"(sleep {SleepProbe.UniqueSleepDuration(15)} &) ; exit 0",
            GenerousTimeout,
            logger,
            TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(0);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("reading stdout"));
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("reading stderr"));
    }

    [Fact]
    public async Task RunAsync_exitsNormally_logsNothing()
    {
        RequireLinux();

        var logger = new RecordingLogger<ExternalProcessTests>();

        await RunShellAsync("exit 0", GenerousTimeout, logger, TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_alreadyCancelled_startsNothing()
    {
        RequireLinux();

        var marker = Path.Combine(Path.GetTempPath(), $"external-process-test-{Guid.NewGuid()}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => RunShellAsync($"touch '{marker}'", GenerousTimeout, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await Task.Delay(200, TestContext.Current.CancellationToken);
        File.Exists(marker).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_stderrOverTailLimit_keepsOnlyTheTail()
    {
        RequireLinux();

        var result = await RunShellAsync(
            $"yes e | head -c {OneMiB} >&2; echo END-MARK >&2",
            GenerousTimeout,
            TestContext.Current.CancellationToken);

        result.StdErrTail.Length.Should().Be(ExternalProcess.StdErrTailChars);
        result.StdErrTail.Should().Contain("END-MARK");
    }

    [Fact]
    public async Task RunAsync_stdOutOverCap_truncatesAndKeepsDraining()
    {
        RequireLinux();

        var result = await RunShellAsync(
            $"yes o | head -c {ExternalProcess.MaxStdOutChars + OneMiB}; exit 7",
            GenerousTimeout,
            TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(7);
        result.StdOut.Should().HaveLength(ExternalProcess.MaxStdOutChars);
    }

    [Fact]
    public async Task RunAsync_toolReadsStdin_seesEndOfInput()
    {
        RequireLinux();

        var result = await RunShellAsync("cat; exit 5", GenerousTimeout, TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(5);
    }

    [Fact]
    public async Task RunAsync_toolMissing_throwsWin32Exception()
    {
        RequireLinux();

        var act = () => ExternalProcess.RunAsync(
            "alexandria-no-such-tool",
            string.Empty,
            GenerousTimeout,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Win32Exception>();
    }
}
