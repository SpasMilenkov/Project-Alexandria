using AwesomeAssertions;
using Builder.Services;
using Builder.Models;
using System.Diagnostics;
using Xunit;

namespace Builder.Tests.Workflow;

public class DockerServiceStreamingTests
{
    private static readonly string ShEchoInterleaved =
        "echo out-1; echo err-1 >&2; sleep 0.05; echo out-2; echo err-2 >&2";

    private static Process StartShell(string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(arguments);
        return Process.Start(info)!;
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
        using var process = StartShell("echo a; echo b >&2");

        DockerService.PumpProcessOutput(process, null);
        process.WaitForExit();

        process.ExitCode.Should().Be(0);
    }

    [Fact]
    public void RunDockerCompose_nonzeroExit_streamsCommandStderrAndExitMarker()
    {
        Func<ProcessStartInfo, Process?> startProcess = _ => StartShell("echo boom-from-fake-docker >&2; exit 7");

        var output = new List<string>();
        var ok = InvokeCompose(startProcess, output);

        ok.Should().BeFalse();
        output[0].Should().StartWith("$ docker compose ");
        output.Should().Contain("boom-from-fake-docker");
        output.Should().Contain("[exit 7]");
    }

    [Fact]
    public void ComposeBuild_relativeTarget_usesAbsolutePathsAndSeparateArguments()
    {
        var launch = Directory.CreateTempSubdirectory("builder launch ");
        var target = Directory.CreateDirectory(Path.Combine(launch.FullName, "settings with spaces"));
        ProcessStartInfo? captured = null;

        Func<ProcessStartInfo, Process?> startProcess = info =>
        {
            captured = info;
            return StartShell("exit 0");
        };

        try
        {
            var sut = CreateSut(startProcess, launch.FullName);

            var succeeded = sut.ComposeBuild("settings with spaces");

            succeeded.Should().BeTrue();
            captured.Should().NotBeNull();
            captured!.WorkingDirectory.Should().Be(target.FullName);
            captured.Arguments.Should().BeEmpty();
            captured.ArgumentList.Should().Equal(
                "compose", "--project-name", "test-project", "--project-directory", target.FullName,
                "-f", Path.Combine(target.FullName, "docker-compose.yml"),
                "--env-file", Path.Combine(target.FullName, ".env"), "build");
        }
        finally
        {
            launch.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetContainerStatuses_relativeTarget_keepsFormatAndPathsAsSingleArguments()
    {
        var launch = Directory.CreateTempSubdirectory("builder status ");
        var target = Directory.CreateDirectory(Path.Combine(launch.FullName, "settings with spaces"));
        ProcessStartInfo? captured = null;

        Func<ProcessStartInfo, Process?> startProcess = info =>
        {
            captured = info;
            return StartShell("echo '{\"Service\":\"api\",\"Name\":\"test-api\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":0}'");
        };

        try
        {
            var sut = CreateSut(startProcess, launch.FullName);

            var statuses = sut.GetContainerStatuses("settings with spaces");

            statuses.Should().ContainSingle().Which.Should().Be("api: running (healthy)");
            captured.Should().NotBeNull();
            captured!.WorkingDirectory.Should().Be(target.FullName);
            captured.Arguments.Should().BeEmpty();
            captured.ArgumentList.Should().Equal(
                "compose", "--project-name", "test-project", "--project-directory", target.FullName,
                "-f", Path.Combine(target.FullName, "docker-compose.yml"),
                "--env-file", Path.Combine(target.FullName, ".env"),
                "ps", "--all", "--format", "json");
        }
        finally
        {
            launch.Delete(recursive: true);
        }
    }

    [Fact]
    public void RunDockerCompose_spawnThrows_streamsErrorLine()
    {
        Func<ProcessStartInfo, Process?> startProcess = _ => throw new InvalidOperationException("spawn failed hard");

        var output = new List<string>();
        var ok = InvokeCompose(startProcess, output);

        ok.Should().BeFalse();
        output.Should().Contain("[error] spawn failed hard");
    }

    [Fact]
    public void RunDockerCompose_startReturnsNull_reportsInsteadOfSilentFalse()
    {
        Func<ProcessStartInfo, Process?> startProcess = _ => null;

        var output = new List<string>();
        var ok = InvokeCompose(startProcess, output);

        ok.Should().BeFalse();
        output.Should().Contain("[error] docker process could not be started");
    }

    private static DockerService CreateSut(Func<ProcessStartInfo, Process?> startProcess, string? launchDirectory = null)
    {
        var sut = new DockerService(new ProcessRunner(startProcess), launchDirectory);
        sut.Configure(new InstallationConfig { ProjectName = "test-project" });
        return sut;
    }

    private static bool InvokeCompose(Func<ProcessStartInfo, Process?> startProcess, List<string> output)
    {
        var workDir = Directory.CreateTempSubdirectory("builder-docker-test").FullName;

        try
        {
            return CreateSut(startProcess).ComposeBuild(workDir, output.Add);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* temp cleanup */ }
        }
    }
}
