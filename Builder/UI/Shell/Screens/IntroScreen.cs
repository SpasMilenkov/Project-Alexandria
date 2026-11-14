using Builder.Preferences;
using Builder.UI.Shell.Input;
using Builder.UI.Shell.Screens.Components;
using Builder.UI.Theming;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// Entry screen: wordmark and main menu. The look can also change anytime
// with T; the menu entry adds a side-by-side preview. Theme persistence
// arrives as a parameter so tests inject a scratch store.
public static class IntroScreen
{
    private const string SetUp = "Set up Alexandria on this machine";
    private const string ChangeLook = "Change the installer's look";
    private const string Production = "Production deployment (coming soon)";

    public static ScreenOutcome Run(
        Shell shell,
        Func<string, bool>? saveTheme = null)
    {
        while (true)
        {
            var header = new Rows(
                Views.Banner(),
                Views.Note("Pick what to do. Nothing is installed until you confirm the final summary."),
                Views.Blank());

            var choice = Widgets.AskSingleSelect(
                shell,
                header,
                "What would you like to do?",
                [SetUp, ChangeLook, Production],
                c => c,
                detailFor: Describe);

            // Esc on the entry screen has nowhere to go back to; re-render
            if (choice is null)
            {
                shell.TakeEscape();
                continue;
            }

            switch (choice)
            {
                case SetUp:
                    return ScreenOutcome.Continue;

                case ChangeLook:
                    PickTheme(shell, saveTheme ?? UiPreferencesStore.Save);
                    break;

                default:
                    ShowComingSoon(shell);

                    return ScreenOutcome.Abort;
            }
        }
    }

    private static IRenderable Describe(string choice)
    {
        var text = choice switch
        {
            SetUp => "A guided local install: features, network address, HTTPS and your admin account.",
            ChangeLook => "Preview the color themes. T switches the look on any screen.",
            _ => "Server deployment from the installer is on the roadmap.",
        };

        return Views.Note(text);
    }

    private static void PickTheme(Shell shell, Func<string, bool> saveTheme)
    {
        var selected = Widgets.AskSingleSelect(
            shell,
            new Rows(Views.Banner(), Views.Heading("Choose a look", "Each look keeps the same layout; only the colors change.")),
            "Looks",
            Themes.All,
            t => t.DisplayName,
            initialIndex: Themes.IndexOf(Theme.Active),
            detailFor: Views.ThemeSample);

        // Esc here means "never mind": keep the current look and go back
        if (selected is null)
        {
            shell.TakeEscape();
            return;
        }

        Theme.Use(selected);

        if (!saveTheme(selected.Id))
        {
            shell.SetMain(new Rows(
                Views.Banner(),
                Views.WarningPanel(
                    "Your look is applied for this session, but it could not be saved. " +
                    $"Next time you run the installer it will start as {Themes.Default.DisplayName} again.")));

            Widgets.WaitForEnter(shell);
            shell.TakeEscape();
        }
    }

    private static void ShowComingSoon(Shell shell)
    {
        shell.SetMain(new Rows(
            Views.Banner(),
            Views.InfoPanel(
                "Coming soon",
                Markup.Escape("Deploying to a server from inside the installer is on the roadmap.\n" +
                              "For now, production setups are documented in the project's manual installation guide."))),
            [new KeyHint("Enter", "exit")]);

        Widgets.WaitForEnter(shell);
    }
}
