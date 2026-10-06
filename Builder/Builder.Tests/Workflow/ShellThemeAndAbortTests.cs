using AwesomeAssertions;
using Builder.Tests.Support;
using Builder.UI;
using Builder.UI.Shell;
using Xunit;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class ShellThemeAndAbortTests
{
    [Fact]
    public void GlobalT_cyclesTheme_andPersistsPerPress()
    {
        var saved = new List<string>();
        using var harness = new ShellHarness(new FakeKeys());

        harness.Shell.ThemeSaver = id => { saved.Add(id); return true; };

        var before = Theme.Active.Id;

        var consumed = harness.Shell.HandleGlobalKey(new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false));

        consumed.Should().BeTrue();
        Theme.Active.Id.Should().NotBe(before);
        saved.Should().ContainSingle().Which.Should().Be(Theme.Active.Id);

        // cycling back returns to the original palette
        harness.Shell.HandleGlobalKey(new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false));
        Theme.Active.Id.Should().Be(before);
    }

    [Fact]
    public void GlobalT_failedSave_surfacesWarningInSidebar()
    {
        using var harness = new ShellHarness(new FakeKeys());

        harness.Shell.ThemeSaver = _ => false;

        harness.Shell.HandleGlobalKey(new ConsoleKeyInfo('t', ConsoleKey.T, false, false, false));

        harness.Shell.AbortRequested.Should().BeFalse();
        harness.Shell.CurrentSidebar.SaveWarning.Should().NotBeNull();
    }

    [Fact]
    public void CtrlC_invokesAbortHook_andThrowsToUnwind()
    {
        using var harness = new ShellHarness(new FakeKeys());
        var aborted = false;

        harness.Shell.OnAbort = () => aborted = true;

        var act = () => harness.Shell.HandleGlobalKey(
            new ConsoleKeyInfo('\u0003', ConsoleKey.C, false, false, true));

        act.Should().Throw<ShellAbortException>();
        aborted.Should().BeTrue();
        harness.Shell.AbortRequested.Should().BeTrue();
    }

    // Regression: ComposeFrame once called Refresh at its tail while Refresh
    // called ComposeFrame - mutual recursion that only fired on real
    // terminals (dry tests returned early). One live-path refresh must
    // complete instead of overflowing the stack.
    [Fact]
    public void Refresh_livePath_terminates()
    {
        using var harness = new ShellHarness(new FakeKeys());

        harness.Shell.EnableLive = true;

        var act = () =>
        {
            harness.Shell.Refresh();
            harness.Shell.SetMain(new Spectre.Console.Markup("frame"));
        };

        act.Should().NotThrow();
    }
}
