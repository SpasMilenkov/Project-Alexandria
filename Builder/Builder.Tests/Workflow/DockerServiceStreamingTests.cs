using AwesomeAssertions;
using Builder.Services;
using System.Diagnostics;
using Xunit;

namespace Builder.Tests.Workflow;

// D54 contract: docker invocations leave evidence - echoed command, both
// pipes drained concurrently, exceptions surfaced, exit codes marked.
public class DockerServiceStreamingTests
{
    private static readonly string ShEchoInterleaved =
        "-c \"echo out-1; echo err-1 >&2; sleep 0.05; echo out-2; echo err-2 >&2\"";

    private static Process StartShell(string arguments)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
    }

    [Fact]
    public void Pump_capturesBothPipes_whenStreamsInterleave()
    {
        using var process = StartShell(ShEchoInterleaved);
        var lines = new List<string>();

        DockerService.PumpProcessOutput(process, lines.Add);
        process.WaitForExit();

        lines.Should().Contain("out-1").And.Contain("err-1")
            .And.Contain("out-2").And.Contain("err-2");
    }

    [Fact]
    public void Pump_withoutCallback_drainsWithoutHanging()
    {
        using var process = StartShell("-c \"echo a; echo b >&2\"");

        DockerService.PumpProcessOutput(process, null);
        process.WaitForExit();

        process.ExitCode.Should().Be(0);
    }

    [Fact]
    public void RunDockerCompose_nonzeroExit_streamsCommandStderrAndExitMarker()
    {
        var capturedInfo = default(ProcessStartInfo);

        DockerService.StartProcess = info =>
        {
            capturedInfo = info;

            return StartShell("-c \"echo boom-from-fake-docker >&2; exit 7\"");
        };

        try
        {
            var output = new List<string>();
            var ok = InvokeCompose(output);

            ok.Should().BeFalse();
            output[0].Should().StartWith("$ docker compose ");
            output.Should().Contain("boom-from-fake-docker");
            output.Should().Contain("[exit 7]");
        }
        finally
        {
            DockerService.StartProcess = info => Process.Start(info);
        }
    }

    [Fact]
    public void RunDockerCompose_spawnThrows_streamsErrorLine()
    {
        DockerService.StartProcess = _ => throw new InvalidOperationException("spawn failed hard");

        try
        {
            var output = new List<string>();
            var ok = InvokeCompose(output);

            ok.Should().BeFalse();
            output.Should().Contain("[error] spawn failed hard");
        }
        finally
        {
            DockerService.StartProcess = info => Process.Start(info);
        }
    }

    [Fact]
    public void RunDockerCompose_startReturnsNull_reportsInsteadOfSilentFalse()
    {
        DockerService.StartProcess = _ => null;

        try
        {
            var output = new List<string>();
            var ok = InvokeCompose(output);

            ok.Should().BeFalse();
            output.Should().Contain("[error] docker process could not be started");
        }
        finally
        {
            DockerService.StartProcess = info => Process.Start(info);
        }
    }

    // Runs the real RunDockerCompose via ComposeBuild; the StartProcess seam
    // replaces the child process so no docker binary is required.
    private static bool InvokeCompose(List<string> output)
    {
        var workDir = Directory.CreateTempSubdirectory("builder-docker-test").FullName;

        try
        {
            return new DockerService().ComposeBuild(workDir, output.Add);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* temp cleanup */ }
        }
    }
}
