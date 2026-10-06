using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public enum ScreenOutcome
{
    Continue,
    Back,
    Abort,
}

// Entry screen: banner + main menu. The palette is no longer gated here -
// the sidebar's [T] and the "Change the installer's look" entry cover it
// (locked decision D32). Theme persistence arrives as a parameter so tests
// inject a scratch store instead of this class reaching for statics.
public static class IntroScreen
{
    public static ScreenOutcome Run(
        Shell shell,
        Func<string, bool>? saveTheme = null)
    {
        while (true)
        {
            var header = new Rows(
                Views.Banner(),
                Views.Note("Pick what to do - you can change your mind before anything is installed."));

            var choice = Widgets.AskSingleSelect(
                shell,
                header,
                "Main menu",
                [
                    "Set up Alexandria on this machine",
                    "Change the installer's look",
                    "Production deployment (coming soon)",
                ],
                c => c);

            // Esc on the entry screen has nowhere to go back to - re-render
            if (choice is null)
            {
                continue;
            }

            switch (choice)
            {
                case "Set up Alexandria on this machine":
                    return ScreenOutcome.Continue;

                case "Change the installer's look":
                    PickTheme(shell, saveTheme ?? UiPreferencesStore.Save);
                    break;

                default:
                    ShowComingSoon(shell);

                    return ScreenOutcome.Abort;
            }
        }
    }

    private static void PickTheme(Shell shell, Func<string, bool> saveTheme)
    {
        var initialIndex = 0;

        for (var i = 0; i < Themes.All.Count; i++)
        {
            if (ReferenceEquals(Themes.All[i], Theme.Active))
            {
                initialIndex = i;
                break;
            }
        }

        var selected = Widgets.AskSingleSelect(
            shell,
            new Rows(Views.Banner(), Views.Note("Which look do you prefer?")),
            "Looks",
            Themes.All,
            t => $"{t.DisplayName}  -  {t.Tagline}",
            initialIndex: initialIndex);

        // Esc here means "never mind" - keep the current look and go back
        if (selected is null)
        {
            return;
        }

        Theme.Use(selected);

        if (!saveTheme(selected.Id))
        {
            shell.SetMain(new Rows(
                Views.Banner(),
                Views.WarningPanel(
                    $"Your look is applied for this session, but it could not be saved. " +
                    $"Next time you run the installer it will start as {Themes.Default.DisplayName} again.")));

            WaitEnter(shell);
        }
    }

    private static void ShowComingSoon(Shell shell)
    {
        shell.SetMain(new Rows(
            Views.Banner(),
            Views.InfoPanel(
                "Coming soon",
                "[dim]Deploying to a server from inside the installer is on the roadmap.\n" +
                "For now, production setups are documented in the project's manual installation guide.[/]")));

        WaitEnter(shell);
    }

    // Returns true when the user pressed Esc - callers decide whether that
    // means going back a screen or just leaving an informational pause
    internal static bool WaitEnter(Shell shell)
    {
        while (true)
        {
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    return false;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return true;
            }
        }
    }
}
