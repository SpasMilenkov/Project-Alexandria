using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using Builder.Workflow;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class IntroScreenTests : IDisposable
{
    private readonly string _prefsDirectory = Path.Combine(
        Path.GetTempPath(), "builder-intro-tests", Guid.NewGuid().ToString("N"));

    public static TheoryData<ThemeDefinition> AllThemes => new(Themes.All);

    [Fact]
    public void Run_setupSelected_continues()
    {
        using var h = new ShellHarness(new FakeKeys().Enter()); // menu: first item

        var outcome = IntroScreen.Run(h.Shell, UiPreferencesStore.Save);

        outcome.Should().Be(ScreenOutcome.Continue);
    }

    [Fact]
    public void Run_productionSelected_showsComingSoonAndAborts()
    {
        using var h = new ShellHarness(new FakeKeys().Down().Down().Enter().Enter());

        var outcome = IntroScreen.Run(h.Shell, UiPreferencesStore.Save);

        outcome.Should().Be(ScreenOutcome.Abort);
        h.Output.Should().Contain("Coming soon");
    }

    [Fact]
    public void Run_changeLook_persistsSelection_thenReturnsToMenu()
    {
        string? savedId = null;

        using var h = new ShellHarness(new FakeKeys()
            .Down().Enter()      // menu: change look -> picker opens on current
            .Down().Enter()      // picker: switch to Aurora, save, back to menu
            .Enter());           // menu: set up

        // Start from Papyrus so the first Down lands on Aurora
        Theme.Use(Themes.Papyrus);

        Func<string, bool> saver = id => { savedId = id; return true; };

        var outcome = IntroScreen.Run(h.Shell, saver);

        outcome.Should().Be(ScreenOutcome.Continue);
        savedId.Should().Be("aurora");
        Theme.Active.Id.Should().Be("aurora");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_prefsDirectory, recursive: true);
        }
        catch
        {
            // temp cleanup is best effort
        }
    }
}
