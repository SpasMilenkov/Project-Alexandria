using System.Diagnostics;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using NSubstitute;

namespace Builder.Tests.Services;

[Collection(nameof(StaticConsoleTests))]
public class SystemCheckerTests
{
    private static SystemChecker CreateSut(
        Func<ProcessStartInfo, Process?> startProcess,
        Func<string, string?>? environment = null,
        TimeSpan? timeout = null) => new(new ProcessRunner(startProcess))
    {
        GetEnvironmentVariable = environment ?? (_ => null),
        ProbeTimeout = timeout ?? TimeSpan.FromSeconds(2),
    };

    private static Process StartShell(string script)
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

        return Process.Start(info)!;
    }

    [Fact]
    public void GetHostPlatformError_glibcLinuxX64_acceptsWithoutStartingProcesses()
    {
        var runner = Substitute.For<IProcessRunner>();
        var sut = new SystemChecker(runner)
        {
            DetectOperatingSystem = () => OSPlatform.Linux,
            DetectArchitecture = () => Architecture.X64,
            DetectGlibc = () => true,
        };

        sut.GetHostPlatformError().Should().BeNull();

        runner.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("Windows", Architecture.X64)]
    [InlineData("OSX", Architecture.X64)]
    [InlineData("Unknown", Architecture.X64)]
    [InlineData("Linux", Architecture.Arm64)]
    [InlineData("Linux", Architecture.Arm)]
    [InlineData("Linux", Architecture.X86)]
    public void GetHostPlatformError_unsupportedHost_rejectsBeforeLibcOrProcessProbe(string platform, Architecture architecture)
    {
        var glibcChecked = false;
        var runner = Substitute.For<IProcessRunner>();
        var os = platform switch
        {
            "Linux" => OSPlatform.Linux,
            "Windows" => OSPlatform.Windows,
            "OSX" => OSPlatform.OSX,
            _ => OSPlatform.Create(platform),
        };
        var sut = new SystemChecker(runner)
        {
            DetectOperatingSystem = () => os,
            DetectArchitecture = () => architecture,
            DetectGlibc = () => glibcChecked = true,
        };

        sut.GetHostPlatformError().Should().Contain("glibc-based Linux x64 hosts only")
            .And.Contain(os.ToString()).And.Contain(architecture.ToString().ToLowerInvariant());

        glibcChecked.Should().BeFalse();
        runner.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void GetHostPlatformError_linuxX64WithoutGlibc_rejectsMusl()
    {
        var sut = new SystemChecker(Substitute.For<IProcessRunner>())
        {
            DetectOperatingSystem = () => OSPlatform.Linux,
            DetectArchitecture = () => Architecture.X64,
            DetectGlibc = () => false,
        };

        sut.GetHostPlatformError().Should().Contain("requires glibc").And.Contain("Alpine Linux");
    }

    [Theory]
    [InlineData("amd64")]
    [InlineData("x86_64")]
    public void GetDockerPlatformError_linuxAmd64Daemon_acceptsServerInformation(string architecture)
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.Run(Arg.Any<ProcessStartInfo>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>(),
            Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<bool>())
            .Returns(new ProcessResult(0, $"{{\"OSType\":\"linux\",\"Architecture\":\"{architecture}\"}}", string.Empty));
        var sut = new SystemChecker(runner);

        sut.GetDockerPlatformError().Should().BeNull();

        runner.Received(1).Run(Arg.Is<ProcessStartInfo>(info => info.FileName == "docker" &&
                info.ArgumentList.SequenceEqual(new[] { "info", "--format", "{\"OSType\":{{json .OSType}},\"Architecture\":{{json .Architecture}}}" })),
            TimeSpan.FromSeconds(10), CancellationToken.None, null, null, true);
    }

    [Theory]
    [InlineData("windows", "x86_64")]
    [InlineData("linux", "aarch64")]
    [InlineData("linux", "arm64")]
    [InlineData("linux", "armv7l")]
    public void GetDockerPlatformError_unsupportedDaemon_rejectsConcreteServerPlatform(string os, string architecture)
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.Run(Arg.Any<ProcessStartInfo>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>(),
            Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<bool>())
            .Returns(new ProcessResult(0, $"{{\"OSType\":\"{os}\",\"Architecture\":\"{architecture}\"}}", string.Empty));
        var sut = new SystemChecker(runner);

        sut.GetDockerPlatformError().Should().Contain("requires a local Linux amd64 Docker daemon")
            .And.Contain($"Detected {os} {architecture}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"OSType\":\"linux\"}")]
    [InlineData("{\"OSType\":\"linux\",\"Architecture\":64}")]
    public void GetDockerPlatformError_unverifiableServerPlatform_failsClosed(string output)
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.Run(Arg.Any<ProcessStartInfo>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>(),
            Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<bool>())
            .Returns(new ProcessResult(0, output, string.Empty));
        var sut = new SystemChecker(runner);

        sut.GetDockerPlatformError().Should().Contain("local Linux amd64 Docker daemon is required");
    }

    [Fact]
    public void GetDockerPlatformError_daemonProbeFails_preservesAccessDiagnostic()
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.Run(Arg.Any<ProcessStartInfo>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>(),
            Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<bool>())
            .Returns(new ProcessResult(1, string.Empty, "permission denied"));
        var sut = new SystemChecker(runner);

        sut.GetDockerPlatformError().Should().Contain("could not be verified").And.Contain("denied access");
    }

    [Fact]
    public void IsDockerComposeInstalled_pluginMissing_doesNotTryStandaloneCompose()
    {
        var invocations = new List<ProcessStartInfo>();
        var sut = CreateSut(info =>
        {
            invocations.Add(info);
            return StartShell("echo plugin-unavailable >&2; exit 1");
        });

        sut.IsDockerComposeInstalled().Should().BeFalse();

        invocations.Should().ContainSingle();
        invocations[0].FileName.Should().Be("docker");
        invocations[0].Arguments.Should().BeEmpty();
        invocations[0].ArgumentList.Should().Equal("compose", "version");
        sut.GetDockerFailureDetails().Should().Contain("plugin-unavailable");
    }

    [Theory]
    [InlineData("permission denied while trying to connect to the Docker API", "denied access")]
    [InlineData("Cannot connect to the Docker daemon", "daemon is unavailable")]
    [InlineData("context missing was not found", "selected context")]
    public void IsDockerDaemonRunning_connectionFails_explainsSpecificCause(string error, string expected)
    {
        var sut = CreateSut(_ => StartShell($"echo '{error}' >&2; exit 1"));

        sut.IsDockerDaemonRunning().Should().BeFalse();

        sut.GetDockerFailureDetails().Should().Contain(expected);
    }

    [Fact]
    public void IsDockerInstalled_spawnFails_preservesUsefulDiagnostic()
    {
        var sut = CreateSut(_ => throw new InvalidOperationException("missing binary"));

        sut.IsDockerInstalled().Should().BeFalse();

        sut.GetDockerFailureDetails().Should().Contain("missing binary");
    }

    [Fact]
    public void IsDockerDaemonRunning_stderrExceedsPipeCapacity_drainsBothPipes()
    {
        var sut = CreateSut(_ => StartShell(
            "i=0; while [ $i -lt 6000 ]; do echo diagnostic-to-fill-the-stderr-pipe >&2; i=$((i + 1)); done; echo 27.0.1"));

        sut.IsDockerDaemonRunning().Should().BeTrue();
    }

    [Fact]
    public void IsDockerDaemonRunning_silentChild_expiresAndTerminatesProbe()
    {
        var childId = 0;
        var sut = CreateSut(_ =>
        {
            var process = StartShell("sleep 10");
            childId = process.Id;
            return process;
        }, timeout: TimeSpan.FromMilliseconds(100));

        sut.IsDockerDaemonRunning().Should().BeFalse();

        sut.GetDockerFailureDetails().Should().Contain("timed out");
        Action findChild = () => Process.GetProcessById(childId);
        findChild.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConfigureCancellation_cancelledSession_propagatesWithoutStartingProbe()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        var sut = CreateSut(_ =>
        {
            called = true;
            throw new InvalidOperationException("must not start");
        });
        sut.ConfigureCancellation(cancellation.Token);

        Action check = () => sut.IsDockerDaemonRunning();

        check.Should().Throw<OperationCanceledException>();
        called.Should().BeFalse();
        sut.GetDockerFailureDetails().Should().BeEmpty();
    }

    [Fact]
    public void GetProcessUsingPort_cancelledProbe_doesNotSwallowCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = new SystemChecker(new ProcessRunner(), cancellation.Token);

        Action check = () => sut.GetProcessUsingPort(8443);

        check.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData("ssh://user@remote.example")]
    [InlineData("tcp://127.0.0.1:2375")]
    public void GetDockerContextError_remoteConnection_rejectsBeforeDaemonContact(string endpoint)
    {
        var called = false;
        var sut = CreateSut(_ =>
        {
            called = true;
            return null;
        }, key => key == "DOCKER_HOST" ? endpoint : null);

        sut.GetDockerContextError().Should().Contain("local source and settings folders");

        called.Should().BeFalse();
    }

    [Fact]
    public void GetDockerContextError_explicitContext_usesItInsteadOfDockerHost()
    {
        ProcessStartInfo? captured = null;
        var sut = CreateSut(info =>
        {
            captured = info;
            return StartShell("echo unix:///var/run/docker.sock");
        }, key => key switch
        {
            "DOCKER_CONTEXT" => "local context",
            "DOCKER_HOST" => "ssh://remote.example",
            _ => null,
        });

        sut.GetDockerContextError().Should().BeNull();

        captured.Should().NotBeNull();
        captured!.ArgumentList.Should().Equal(
            "context", "inspect", "--format", "{{.Endpoints.docker.Host}}", "local context");
    }

    [Fact]
    public void GetDockerContextError_selectedContextIsRemote_rejectsLocalBindAssumption()
    {
        var invocations = new List<ProcessStartInfo>();
        var sut = CreateSut(info =>
        {
            invocations.Add(info);
            return StartShell(info.ArgumentList.Contains("show")
                ? "echo remote-context"
                : "echo ssh://remote.example");
        });

        sut.GetDockerContextError().Should().Contain("not supported");

        invocations.Should().HaveCount(2);
        invocations[0].ArgumentList.Should().Equal("context", "show");
        invocations[1].ArgumentList.Should().Equal(
            "context", "inspect", "--format", "{{.Endpoints.docker.Host}}", "remote-context");
    }

    [Fact]
    public void GetDockerContextError_contextCannotBeInspected_reportsContextFailure()
    {
        var sut = CreateSut(_ => StartShell("echo context-not-found >&2; exit 1"),
            key => key == "DOCKER_CONTEXT" ? "missing" : null);

        sut.GetDockerContextError().Should().Contain("selected Docker context is unavailable");
    }

    [Fact]
    public void Run_retryAfterDockerBecomesAvailable_preservesWizardAndContinues()
    {
        var checker = Substitute.For<ISystemChecker>();
        checker.GetOperatingSystem().Returns(System.Runtime.InteropServices.OSPlatform.Linux);
        checker.IsDockerInstalled().Returns(false, true);
        checker.IsDockerComposeInstalled().Returns(true);
        checker.IsDockerDaemonRunning().Returns(true);
        checker.GetDockerVersion().Returns("Docker version 27.0.1");
        checker.GetDockerComposeVersion().Returns("Docker Compose version v2.29.0");
        checker.GetSystemResources().Returns(new SystemResources { CpuCores = 8, TotalMemoryMb = 16384 });

        using var harness = new ShellHarness(new FakeKeys().Text("r").Enter());
        harness.Context.Features = FeatureSelection.FromDefaults();

        var outcome = SystemCheckScreen.Run(harness.Shell, checker, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.ShouldAbort.Should().BeFalse();
        checker.Received(2).IsDockerInstalled();
    }

    [Fact]
    public void Run_failedPreflightEscape_returnsToPreviousScreen()
    {
        var checker = Substitute.For<ISystemChecker>();
        checker.GetOperatingSystem().Returns(System.Runtime.InteropServices.OSPlatform.Linux);
        checker.IsDockerInstalled().Returns(false);
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        var outcome = SystemCheckScreen.Run(harness.Shell, checker, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Back);
        harness.Context.ShouldAbort.Should().BeFalse();
    }
}
