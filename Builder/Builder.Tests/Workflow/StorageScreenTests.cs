using AwesomeAssertions;
using Builder.Models;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public sealed class StorageScreenTests
{
    [Fact]
    public void Run_freshDefault_reviewsTenGb()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());

        StorageScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Storage.GarageCapacityGb.Should().Be(10);
        harness.Context.Config.Storage.CapacityReviewed.Should().BeTrue();
        harness.Output.Should().Contain("does not reserve disk space");
    }

    [Fact]
    public void Run_customMinimum_acceptsOneGb()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.U, 'u', ctrl: true).Text("1").Enter());

        StorageScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Storage.GarageCapacityGb.Should().Be(1);
    }

    [Fact]
    public void Run_repair_showsActualCapacityAndPreservesLayout()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.IsRepair = true;
        harness.Context.Config.Storage = new StorageSettings { ActualGarageCapacityBytes = 25_000_000_000 };

        StorageScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Storage.ActualGarageCapacityBytes.Should().Be(25_000_000_000);
        harness.Context.Config.Storage.CapacityReviewed.Should().BeFalse();
        harness.Output.Should().Contain("25 GB").And.Contain("preserved").And.Contain("unknown");
    }

    [Fact]
    public void Run_escape_preservesUnsubmittedCapacity()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.U, 'u', ctrl: true).Text("50").Press(ConsoleKey.Escape));

        StorageScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);

        harness.Context.Config.Storage.GarageCapacityGb.Should().Be(10);
    }
}
