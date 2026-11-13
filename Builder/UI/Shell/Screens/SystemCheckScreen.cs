using Builder.Models;
using Builder.Services;
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
        var checks = new List<(string Label, bool Ok, string? Detail)>();

        // Keys only apply once the checks finish and something follows them
        void Render(params Func<IRenderable>[] after)
        {
            shell.SetMain(() => new Rows([Heading(), Views.CheckList(checks), .. after.Select(build => build())]),
                after.Length == 0 ? [] : null);
        }

        Render();

        var hostError = checker.GetHostPlatformError();

        if (!string.IsNullOrWhiteSpace(hostError))
        {
            return Fail(shell, checks, "Host platform unsupported", hostError);
        }

        checks.Add(("Host platform", true, "Linux x64 (glibc)"));
        Render();

        if (!checker.IsDockerInstalled())
        {
            return Fail(shell, checks, "Docker is not installed or cannot run",
                FailureDetails(checker, "The Docker command must be available in your terminal before continuing."));
        }

        checks.Add(("Docker", true, ShortVersion(checker.GetDockerVersion())));
        Render();

        if (!checker.IsDockerComposeInstalled())
        {
            return Fail(shell, checks, "Docker Compose plugin unavailable",
                FailureDetails(checker, "This installer requires the docker compose command. " +
                                        "The standalone docker-compose command cannot be used."));
        }

        checks.Add(("Docker Compose", true, ShortVersion(checker.GetDockerComposeVersion())));
        Render();

        var contextError = checker.GetDockerContextError();

        if (!string.IsNullOrWhiteSpace(contextError))
        {
            return Fail(shell, checks, "Docker connection cannot use local folders", contextError);
        }

        if (!checker.IsDockerDaemonRunning())
        {
            return Fail(shell, checks, "Docker connection unavailable",
                FailureDetails(checker, "Start Docker on this machine and check that your account has Docker access."));
        }

        checks.Add(("Docker daemon", true, "running"));
        Render();

        var daemonPlatformError = checker.GetDockerPlatformError();

        if (!string.IsNullOrWhiteSpace(daemonPlatformError))
        {
            return Fail(shell, checks, "Docker platform unsupported or unavailable", daemonPlatformError);
        }

        checks.Add(("Docker platform", true, "Linux amd64"));
        Render();

        var resources = checker.GetSystemResources();

        shell.Context.Resources = resources;

        var cpu = resources.DockerCpuCores ?? resources.CpuCores;
        var memory = resources.DockerMemoryMb ?? resources.TotalMemoryMb;
        checks.Add((resources.DockerCpuCores.HasValue ? "Docker CPU" : "Host CPU (Docker limit unknown)", cpu >= 2, $"{cpu} cores"));

        if (memory > 0)
        {
            checks.Add((resources.DockerMemoryMb.HasValue ? "Docker memory" : "Host memory (Docker limit unknown)", memory >= 2048, $"{memory / 1024.0:0.#} GiB"));
        }

        var after = new List<Func<IRenderable>> { Views.Blank };

        if (resources.DockerAvailableDiskMb.HasValue)
        {
            checks.Add(("Docker disk free", resources.DockerAvailableDiskMb >= 10240,
                $"{resources.DockerAvailableDiskMb / 1024.0:0.#} GiB at {resources.DockerDataDirectory}"));
        }
        else
        {
            after.Add(() => Views.Note("Docker disk free: unknown. " + resources.DiskSpaceDetails));
        }

        var warnings = calculator.GetWarnings(resources, shell.Context.Features).ToList();

        if (warnings.Count > 0)
        {
            after.Add(() => Views.WarningPanel(string.Join("\n", warnings)));
        }
        else
        {
            after.Add(() => Views.SuccessPanel("Ready to continue", Markup.Escape("This machine meets the requirements for your selection.")));
        }

        Render([.. after]);

        if (Widgets.WaitForEnter(shell))
        {
            return ScreenOutcome.Back;
        }

        return ScreenOutcome.Continue;
    }

    private static IRenderable Heading() => Views.Heading("Checking your machine",
        "Alexandria runs in Docker. These checks confirm it can build and start here.");

    private static ScreenOutcome? Fail(Shell shell,
        List<(string Label, bool Ok, string? Detail)> checks, string title, string body)
    {
        shell.SetMain(() => new Rows(Heading(), Views.CheckList(checks), Views.Blank(),
                Views.ErrorPanel(title, Markup.Escape(body)),
                Views.Note("Fix the issue, then check again. Nothing has been installed.")),
            [new KeyHint("R", "check again"), new KeyHint("Esc", "back"), new KeyHint("Enter", "exit")]);

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
