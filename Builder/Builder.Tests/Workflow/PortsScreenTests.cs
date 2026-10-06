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

        foreach (var port in busyPorts)
        {
            checker.IsPortAvailable(port).Returns(false);
        }

        checker.GetProcessUsingPort(Arg.Any<int>()).Returns("some-process");

        return checker;
    }

    [Fact]
    public void Run_allPortsFree_storesDefaults()
    {
        using var h = new ShellHarness(new FakeKeys().Enter());

        h.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, false);

        var outcome = PortsScreen.Run(h.Shell, new PortResolver(CheckerWith()), CheckerWith());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.Config.Ports["HTTP_PORT"].Should().Be(80);
        h.Context.Config.Ports["HTTPS_PORT"].Should().Be(443);
    }

    [Fact]
    public void Run_allPortsFree_escapeReturnsBack()
    {
        using var h = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        h.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, false);

        var outcome = PortsScreen.Run(h.Shell, new PortResolver(CheckerWith()), CheckerWith());

        outcome.Should().Be(ScreenOutcome.Back);
    }

    [Fact]
    public void Run_conflictWithAutoRemap_fallsBackTo8080()
    {
        // 80 is busy; everything else free. Auto-remap should land on 8080.
        var checker = CheckerWith(80);
        using var h = new ShellHarness(new FakeKeys().Enter().Enter()); // choose remap, then confirm assignment

        h.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, false);

        var outcome = PortsScreen.Run(h.Shell, new PortResolver(checker), checker);

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.Config.Ports["HTTP_PORT"].Should().Be(8080);
        h.Context.Config.OriginalPorts["HTTP_PORT"].Should().Be(80);

        // The chosen assignment must be shown, not silently stored
        h.Output.Should().Contain("8080");
        h.Output.Should().Contain("Here are the ports Alexandria will use");
    }

    [Fact]
    public void Run_explainsWhatPortsAre_inPlainLanguage()
    {
        using var h = new ShellHarness(new FakeKeys().Enter());

        h.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, false);

        PortsScreen.Run(h.Shell, new PortResolver(), CheckerWith());

        h.Output.Should().Contain("numbered"); // explainer present (wrap-safe words)
        h.Output.Should().Contain("slots");
        h.Output.Should().NotContain("door");
    }

    [Fact]
    public void Run_manualEntry_acceptsOnlyFreePorts()
    {
        var checker = CheckerWith(80);

        using var harness = new ShellHarness(new FakeKeys()
            .Down().Enter()                       // choose "I will type free ports"
            .Text("9000").Enter()                 // manual port for HTTP
            .Enter());                            // confirm final assignment

        harness.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, false);

        var outcome = PortsScreen.Run(harness.Shell, new PortResolver(checker), checker);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.Config.Ports["HTTP_PORT"].Should().Be(9000);
    }
}
