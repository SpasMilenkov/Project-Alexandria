using Builder.Models;
using Builder.Services;
using Builder.UI;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class SystemCheckScreen
{
    public static ScreenOutcome Run(
        Shell shell,
        ISystemChecker checker,
        IResourceCalculator calculator)
    {
        var rows = new List<IRenderable>
        {
            Views.Title("Checking your machine"),
            new Rule().RuleStyle(Theme.Active.BorderStyle),
        };

        void Render()
        {
            shell.SetMain(new Rows(rows.ToArray()));
        }

        Render();

        var os = checker.GetOperatingSystem();

        rows.Add(Views.CheckRow("Operating system", true, os.ToString()));
        Render();

        if (!checker.IsDockerInstalled())
        {
            return Fail(shell, rows, "Docker is not installed",
                $"Install it with: [bold]{Markup.Escape(checker.GetDockerInstallInstructions().command)}[/]\n" +
                $"Docs: {checker.GetDockerInstallInstructions().url}");
        }

        rows.Add(Views.CheckRow("Docker", true, ShortVersion(checker.GetDockerVersion())));
        Render();

        if (!checker.IsDockerDaemonRunning())
        {
            return Fail(shell, rows, "Docker is installed but not running",
                "Start Docker Desktop (Windows/macOS) or run: sudo systemctl start docker");
        }

        rows.Add(Views.CheckRow("Docker daemon", true, "running"));
        Render();

        if (!checker.IsDockerComposeInstalled())
        {
            return Fail(shell, rows, "Docker Compose missing",
                "Docker Compose v2 ships with recent Docker versions.");
        }

        rows.Add(Views.CheckRow("Docker Compose", true, ShortVersion(checker.GetDockerComposeVersion())));
        Render();

        var resources = checker.GetSystemResources();

        shell.Context.Resources = resources;

        rows.Add(Views.CheckRow("CPU", resources.CpuCores >= 2, $"{resources.CpuCores} cores"));

        if (resources.TotalMemoryMb > 0)
        {
            rows.Add(Views.CheckRow("Memory", resources.TotalMemoryMb >= 2048, $"{resources.TotalMemoryMb / 1024.0:0.#} GB"));
        }

        if (resources.AvailableDiskMb > 0)
        {
            rows.Add(Views.CheckRow("Disk free", resources.AvailableDiskMb >= 10240, $"{resources.AvailableDiskMb / 1024.0:0.#} GB"));
        }

        foreach (var warning in calculator.GetWarnings(resources, shell.Context.Features))
        {
            rows.Add(new Markup($"  [{Theme.Active.Wa}]![/] [dim]{Markup.Escape(warning)}[/]"));
        }

        rows.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));
        rows.Add(Views.Note("All good? Press Enter to continue - Esc takes you back."));
        Render();

        if (IntroScreen.WaitEnter(shell))
        {
            return ScreenOutcome.Back;
        }

        return ScreenOutcome.Continue;
    }

    private static ScreenOutcome Fail(Shell shell, List<IRenderable> rows, string title, string body)
    {
        rows.Add(Views.ErrorPanel(title, body));
        rows.Add(Views.Note("Press Enter to exit - fix the issue above and try again."));
        shell.SetMain(new Rows(rows.ToArray()));

        IntroScreen.WaitEnter(shell);
        shell.Context.ShouldAbort = true;

        return ScreenOutcome.Abort;
    }

    private static string ShortVersion(string versionLine)
    {
        // docker version strings are long; keep the useful head only
        var parts = versionLine.Split(',');

        return parts.Length > 1 ? parts[0] + "," : versionLine;
    }
}
