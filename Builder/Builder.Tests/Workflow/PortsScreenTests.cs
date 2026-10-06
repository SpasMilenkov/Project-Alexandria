using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using NSubstitute;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class PortsScreenTests
{
    private static ISystemChecker CheckerWith(params int[] busyPorts)
    {
        var checker = Substitute.For<ISystemChecker>();
        checker.IsPortAvailable(Arg.Any<int>()).Returns(true);

        foreach (var port in busyPorts) checker.IsPortAvailable(port).Returns(false);

        checker.GetProcessUsingPort(Arg.Any<int>()).Returns("some-process");

        return checker;
    }

    [Fact]
    public void Run_freeDefault_publishesOnlyHttpsAndKeepsLanHost()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Enter());
        var checker = CheckerWith();

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Ports.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, int>("HTTPS_PORT", 443));
        harness.Context.Config.PublicEndpoint.Origin.Should().Be("https://192.0.2.10");
        harness.Output.Should().Contain("one");
        harness.Output.Should().Contain("internal services stay private");
    }

    [Fact]
    public void Run_hostCaptureEscape_returnsBackWithoutSubmitting()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        PortsScreen.Run(harness.Shell, new PortResolver(CheckerWith()), CheckerWith()).Should().Be(ScreenOutcome.Back);
    }

    [Fact]
    public void Run_busy443_autoRemapShows8443BeforeConfirmation()
    {
        var checker = CheckerWith(443);
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Enter());

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.PublicEndpoint.Origin.Should().Be("https://192.0.2.10:8443");
        harness.Context.Config.OriginalPorts["HTTPS_PORT"].Should().Be(443);
        harness.Output.Should().Contain("https://192.0.2.10:8443");
    }

    [Fact]
    public void Run_defaultFree_stillAllowsCustomPort()
    {
        var checker = CheckerWith();
        using var harness = new ShellHarness(new FakeKeys().Enter().Down().Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text("9000").Enter().Enter());

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Ports.Should().ContainSingle();
        harness.Context.Config.PublicEndpoint.Origin.Should().Be("https://192.0.2.10:9000");
    }

    [Fact]
    public void Run_manualBusyPort_rejectsUntilFreePortEntered()
    {
        var checker = CheckerWith(443, 9000);
        using var harness = new ShellHarness(new FakeKeys().Enter().Down().Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text("9000").Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text("9443").Enter().Enter());

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.PublicEndpoint.HttpsPort.Should().Be(9443);
        harness.Output.Should().Contain("already in use");
    }

    [Fact]
    public void Run_repairWithBoundSavedPort_keepsItsAssignment()
    {
        var checker = CheckerWith(443, 9443);
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Enter());
        harness.Context.IsRepair = true;
        harness.Context.OwnedHttpsPort = 9443;
        harness.Context.Config.Ports = new() { ["HTTP_PORT"] = 80, ["HTTPS_PORT"] = 9443, ["GRAFANA_PORT"] = 3000 };

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Ports.Should().ContainSingle();
        harness.Context.Config.PublicEndpoint.HttpsPort.Should().Be(9443);
    }

    [Fact]
    public void Run_returnToDraft_keepsCustomHostAndPort()
    {
        var checker = CheckerWith();
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter().Enter());
        harness.Context.Config.PublicEndpoint = new PublicEndpoint { Host = "alexandria.example", HttpsPort = 9443 };
        harness.Context.Config.Ports = new() { ["HTTPS_PORT"] = 9443 };

        PortsScreen.Run(harness.Shell, new PortResolver(checker), checker).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.PublicEndpoint.Origin.Should().Be("https://alexandria.example:9443");
    }
}
