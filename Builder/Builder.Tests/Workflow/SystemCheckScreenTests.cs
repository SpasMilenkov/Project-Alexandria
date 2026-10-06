using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using NSubstitute;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class SystemCheckScreenTests
{
    private static ISystemChecker HealthyChecker()
    {
        var checker = Substitute.For<ISystemChecker>();

        checker.GetOperatingSystem().Returns(System.Runtime.InteropServices.OSPlatform.Linux);
        checker.IsDockerInstalled().Returns(true);
        checker.IsDockerDaemonRunning().Returns(true);
        checker.IsDockerComposeInstalled().Returns(true);
        checker.GetDockerVersion().Returns("Docker version 27.0.1, build abc");
        checker.GetDockerComposeVersion().Returns("Docker Compose version v2.29.0");

        checker.GetSystemResources().Returns(new SystemResources
        {
            CpuCores = 8,
            TotalMemoryMb = 16384,
            AvailableDiskMb = 51200,
        });

        return checker;
    }

    [Fact]
    public void Run_healthySystem_continues()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());

        harness.Context.Features = Builder.Models.FeatureSelection.FromDefaults();

        var outcome = SystemCheckScreen.Run(harness.Shell, HealthyChecker(), new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Output.Should().Contain("Checking your machine");
    }

    [Fact]
    public void Run_dockerMissing_showsInstructionsAndAborts()
    {
        var checker = Substitute.For<ISystemChecker>();

        checker.GetOperatingSystem().Returns(System.Runtime.InteropServices.OSPlatform.Linux);
        checker.IsDockerInstalled().Returns(false);

        checker.GetDockerInstallInstructions()
            .Returns(("curl -fsSL https://get.docker.com | sh", "https://docs.docker.com/engine/install/"));

        using var h = new ShellHarness(new FakeKeys().Enter());

        h.Context.Features = Builder.Models.FeatureSelection.FromDefaults();

        var outcome = SystemCheckScreen.Run(h.Shell, checker, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Abort);
        h.Context.ShouldAbort.Should().BeTrue();
        h.Output.Should().Contain("Docker is not installed");
    }
}
