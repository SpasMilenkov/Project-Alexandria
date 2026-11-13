using AwesomeAssertions;
using Builder.Tests.Support;
using Spectre.Console;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class ShellScrollTests
{
    private static readonly ConsoleKeyInfo PageUp = new('\0', ConsoleKey.PageUp, false, false, false);

    [Fact]
    public void PageUp_liveContentTallerThanWindow_scrollsInsteadOfReachingTheScreen()
    {
        using var harness = new ShellHarness(new FakeKeys());
        harness.Console.Profile.Height = 12;
        harness.Shell.EnableLive = true;

        harness.Shell.SetMain(new Markup(string.Join("\n", Enumerable.Range(1, 40).Select(i => $"line {i}"))));

        harness.Shell.HandleGlobalKey(PageUp).Should().BeTrue();
    }

    [Fact]
    public void PageUp_contentFits_isLeftForTheScreen()
    {
        using var harness = new ShellHarness(new FakeKeys());
        harness.Console.Profile.Height = 40;
        harness.Shell.EnableLive = true;

        harness.Shell.SetMain(new Markup("short page"));

        harness.Shell.HandleGlobalKey(PageUp).Should().BeFalse();
    }

    [Fact]
    public void DryOutput_tallContent_printsEveryLine()
    {
        using var harness = new ShellHarness(new FakeKeys());
        harness.Console.Profile.Height = 12;

        harness.Shell.SetMain(new Markup(string.Join("\n", Enumerable.Range(1, 40).Select(i => $"line {i}"))));

        harness.Output.Should().Contain("line 1").And.Contain("line 40");
        harness.Shell.HandleGlobalKey(PageUp).Should().BeFalse();
    }
}
