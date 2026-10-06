using System.Diagnostics;
using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class ProcessRunnerTests
{
    private static ProcessStartInfo Shell(string script)
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return info;
    }

    [Fact]
    public void Run_saturatedOutputAndErrorPipes_drainsBothAndRetainsExitCode()
    {
        var sut = new ProcessRunner();
        var info = Shell("i=0; while [ $i -lt 6000 ]; do echo output-line; echo error-line >&2; i=$((i+1)); done; exit 7");

        var result = sut.Run(info, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(7);
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(6000);
        result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(6000);
    }

    [Fact]
    public void Run_cancellationDuringOutput_killsChildAndPropagatesCancellation()
    {
        var childId = 0;
        var sut = new ProcessRunner(info =>
        {
            var process = Process.Start(info)!;
            childId = process.Id;
            return process;
        });
        using var cancellation = new CancellationTokenSource();

        Action run = () => sut.Run(Shell("echo ready; sleep 10"), TimeSpan.FromSeconds(5), cancellation.Token,
            _ => cancellation.Cancel());

        run.Should().Throw<OperationCanceledException>();
        Action find = () => Process.GetProcessById(childId);
        find.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Run_silentChildTimeout_killsTreeAndReturnsNamedTimeout()
    {
        var sut = new ProcessRunner();

        var result = sut.Run(Shell("sleep 10"), TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        result.TimedOut.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Run_outputConsumerThrows_terminatesChildAndPreservesFailure()
    {
        var sut = new ProcessRunner();

        Action run = () => sut.Run(Shell("echo ready; sleep 10"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken,
            _ => throw new InvalidOperationException("consumer failed"));

        run.Should().Throw<InvalidOperationException>().WithMessage("consumer failed");
    }

    [Fact]
    public void Run_silentSuccessfulChild_completesWithoutAnOutputCallback()
    {
        var result = new ProcessRunner().Run(Shell("exit 0"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public void Run_stderrOnlyChild_capturesFinalUnterminatedLine()
    {
        var lines = new List<string>();

        var result = new ProcessRunner().Run(Shell("printf last-diagnostic >&2; exit 4"), TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken, onStandardError: lines.Add);

        result.ExitCode.Should().Be(4);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.TrimEnd().Should().Be("last-diagnostic");
        lines.Should().Equal("last-diagnostic");
    }

    [Fact]
    public void Run_spawnFails_preservesTheOriginalException()
    {
        var original = new InvalidOperationException("cannot spawn child");
        var sut = new ProcessRunner(_ => throw original);

        Action run = () => sut.Run(Shell("exit 0"), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        run.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(original);
    }

    [Fact]
    public void Run_alreadyCancelled_doesNotSpawnAChild()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        var sut = new ProcessRunner(_ =>
        {
            called = true;
            throw new InvalidOperationException("must not spawn");
        });

        Action run = () => sut.Run(Shell("exit 0"), TimeSpan.FromSeconds(2), cancellation.Token);

        run.Should().Throw<OperationCanceledException>();
        called.Should().BeFalse();
    }

    [Fact]
    public void Run_timeoutAfterOutput_retainsEvidenceAndStopsDescendant()
    {
        var descendantId = 0;
        var timer = Stopwatch.StartNew();

        var result = new ProcessRunner().Run(Shell("sleep 30 & child=$!; echo $child; echo before-timeout >&2; wait $child"),
            TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken, line => descendantId = int.Parse(line));

        result.TimedOut.Should().BeTrue();
        result.StandardError.Should().Contain("before-timeout");
        descendantId.Should().BeGreaterThan(0);
        IsRunning(descendantId).Should().BeFalse();
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Run_timeoutInterruptsAnUnterminatedLine_drainsItsRemainingText()
    {
        var result = new ProcessRunner().Run(Shell("printf interrupted-fragment; sleep 30"), TimeSpan.FromMilliseconds(200),
            TestContext.Current.CancellationToken);

        result.TimedOut.Should().BeTrue();
        result.StandardOutput.TrimEnd().Should().Be("interrupted-fragment");
    }

    [Fact]
    public void Run_cancellation_stopsWaitingParentAndItsDescendantBeforeReturning()
    {
        var parentId = 0;
        var descendantId = 0;
        var sut = new ProcessRunner(info =>
        {
            var process = Process.Start(info)!;
            parentId = process.Id;
            return process;
        });
        using var cancellation = new CancellationTokenSource();
        var timer = Stopwatch.StartNew();

        Action run = () => sut.Run(Shell("sleep 30 & child=$!; echo $child; wait $child"), TimeSpan.FromSeconds(5),
            cancellation.Token, line =>
            {
                descendantId = int.Parse(line);
                cancellation.Cancel();
            });

        run.Should().Throw<OperationCanceledException>();
        descendantId.Should().BeGreaterThan(0);
        IsRunning(parentId).Should().BeFalse();
        IsRunning(descendantId).Should().BeFalse();
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Run_errorConsumerThrows_stopsChildAndRetainsExceptionIdentity()
    {
        var original = new InvalidOperationException("stderr consumer failed");

        Action run = () => new ProcessRunner().Run(Shell("echo stderr >&2; sleep 30"), TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken, onStandardError: _ => throw original);

        run.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(original);
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (process.HasExited) return false;

            if (OperatingSystem.IsLinux())
            {
                var status = File.ReadAllText($"/proc/{processId}/stat");
                var state = status[(status.LastIndexOf(')') + 2)..].Split(' ')[0];
                return state is not "Z" and not "X";
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }
}
