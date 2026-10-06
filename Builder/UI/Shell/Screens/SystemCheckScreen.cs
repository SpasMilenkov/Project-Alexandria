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
        while (true)
        {
            var outcome = RunChecks(shell, checker, calculator);

            if (outcome.HasValue)
            {
                return outcome.Value;
            }
        }
    }

    private static ScreenOutcome? RunChecks(
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

        var hostError = checker.GetHostPlatformError();

        if (!string.IsNullOrWhiteSpace(hostError))
        {
            return Fail(shell, rows, "Host platform unsupported", hostError);
        }

        rows.Add(Views.CheckRow("Host platform", true, "Linux x64 (glibc)"));
        Render();

        if (!checker.IsDockerInstalled())
        {
            return Fail(shell, rows, "Docker is not installed or cannot run",
                FailureDetails(checker, "The Docker command must be available in your terminal before continuing."));
        }

        rows.Add(Views.CheckRow("Docker", true, ShortVersion(checker.GetDockerVersion())));
        Render();

        if (!checker.IsDockerComposeInstalled())
        {
            return Fail(shell, rows, "Docker Compose plugin unavailable",
                FailureDetails(checker, "This installer requires the docker compose command. " +
                                        "The standalone docker-compose command cannot be used."));
        }

        rows.Add(Views.CheckRow("Docker Compose", true, ShortVersion(checker.GetDockerComposeVersion())));
        Render();

        var contextError = checker.GetDockerContextError();

        if (!string.IsNullOrWhiteSpace(contextError))
        {
            return Fail(shell, rows, "Docker connection cannot use local folders", contextError);
        }

        if (!checker.IsDockerDaemonRunning())
        {
            return Fail(shell, rows, "Docker connection unavailable",
                FailureDetails(checker, "Start Docker on this machine and check that your account has Docker access."));
        }

        rows.Add(Views.CheckRow("Docker daemon", true, "running"));
        Render();

        var daemonPlatformError = checker.GetDockerPlatformError();

        if (!string.IsNullOrWhiteSpace(daemonPlatformError))
        {
            return Fail(shell, rows, "Docker platform unsupported or unavailable", daemonPlatformError);
        }

        rows.Add(Views.CheckRow("Docker platform", true, "Linux amd64"));
        Render();

        var resources = checker.GetSystemResources();

        shell.Context.Resources = resources;

        var cpu = resources.DockerCpuCores ?? resources.CpuCores;
        var memory = resources.DockerMemoryMb ?? resources.TotalMemoryMb;
        rows.Add(Views.CheckRow(resources.DockerCpuCores.HasValue ? "Docker CPU" : "Host CPU (Docker limit unknown)", cpu >= 2, $"{cpu} cores"));

        if (memory > 0)
        {
            rows.Add(Views.CheckRow(resources.DockerMemoryMb.HasValue ? "Docker memory" : "Host memory (Docker limit unknown)", memory >= 2048, $"{memory / 1024.0:0.#} GiB"));
        }

        if (resources.DockerAvailableDiskMb.HasValue)
        {
            rows.Add(Views.CheckRow("Docker disk free", resources.DockerAvailableDiskMb >= 10240,
                $"{resources.DockerAvailableDiskMb / 1024.0:0.#} GiB at {resources.DockerDataDirectory}"));
        }
        else
        {
            rows.Add(Views.Note("Docker disk free: unknown. " + resources.DiskSpaceDetails));
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

    private static ScreenOutcome? Fail(Shell shell, List<IRenderable> rows, string title, string body)
    {
        rows.Add(Views.ErrorPanel(title, Markup.Escape(body)));
        rows.Add(Views.Note("Fix the issue, then press R to check again. Esc goes back; Enter exits."));
        shell.SetMain(new Rows(rows.ToArray()));

        while (true)
        {
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            if (key.Key == ConsoleKey.R)
            {
                return null;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                return ScreenOutcome.Back;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                shell.Context.ShouldAbort = true;
                return ScreenOutcome.Abort;
            }
        }
    }

    private static string FailureDetails(ISystemChecker checker, string fallback)
    {
        var details = checker.GetDockerFailureDetails();

        return string.IsNullOrWhiteSpace(details) ? fallback : $"{fallback}\n{details}";
    }

    private static string ShortVersion(string versionLine)
    {
        // docker version strings are long; keep the useful head only
        var parts = versionLine.Split(',');

        return parts.Length > 1 ? parts[0] + "," : versionLine;
    }
}
