using Builder.Catalog;
using Builder.SystemCheck;
using Builder.UI.Shell.Input;
using Builder.UI.Shell.Screens.Components;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// Two groups: Docker checks that must pass, then whether Docker has room for
// the selected features. Every row is listed from the start so the user can
// see what is still to come.
public static class SystemCheckScreen
{
    private const int HostRow = 0;
    private const int DockerRow = 1;
    private const int ComposeRow = 2;
    private const int DaemonRow = 3;
    private const int PlatformRow = 4;
    private const int CpuRow = 5;
    private const int MemoryRow = 6;
    private const int DiskRow = 7;
    private const int DockerRowCount = 5;

    private static readonly string[] Labels =
        ["Host platform", "Docker", "Compose plugin", "Daemon", "Platform", "CPU", "Memory", "Disk"];

    private static readonly int LabelWidth = Labels.Max(label => label.Length);

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
        var rows = Labels.Select(label => new CheckRow { Label = label }).ToArray();

        // The frame timer rebuilds a running page off this thread, so each
        // page gets its own copy of the rows
        void ShowProgress()
        {
            var snapshot = rows.ToArray();

            shell.SetMain(() => Page(snapshot, []), [], animated: true);
        }

        void Start(params int[] indexes)
        {
            foreach (var index in indexes)
            {
                rows[index] = rows[index] with { State = CheckState.Running, Detail = "checking…" };
            }

            ShowProgress();
        }

        void Pass(int index, string detail)
        {
            rows[index] = rows[index] with { State = CheckState.Passed, Detail = detail };
        }

        ScreenOutcome? Fail(int index, string detail, string title, string body)
        {
            rows[index] = rows[index] with { State = CheckState.Failed, Detail = detail };

            for (var i = index + 1; i < rows.Length; i++)
            {
                rows[i] = rows[i] with { State = CheckState.Skipped, Detail = "skipped" };
            }

            return ShowFailure(shell, rows.ToArray(), title, body);
        }

        Start(HostRow);

        var hostError = checker.GetHostPlatformError();

        if (!string.IsNullOrWhiteSpace(hostError))
        {
            return Fail(HostRow, "unsupported", "Host platform unsupported", hostError);
        }

        Pass(HostRow, "Linux x64 (glibc)");
        Start(DockerRow);

        if (!checker.IsDockerInstalled())
        {
            return Fail(DockerRow, "not found", "Docker is not installed or cannot run",
                FailureDetails(checker, "The Docker command must be available in your terminal before continuing."));
        }

        Pass(DockerRow, ShortVersion(checker.GetDockerVersion()));
        Start(ComposeRow);

        if (!checker.IsDockerComposeInstalled())
        {
            return Fail(ComposeRow, "not found", "Docker Compose plugin unavailable",
                FailureDetails(checker, "This installer requires the docker compose command. " +
                                        "The standalone docker-compose command cannot be used."));
        }

        Pass(ComposeRow, ShortVersion(checker.GetDockerComposeVersion()));
        Start(DaemonRow);

        var contextError = checker.GetDockerContextError();

        if (!string.IsNullOrWhiteSpace(contextError))
        {
            return Fail(DaemonRow, "connection unsupported", "Docker connection cannot use local folders", contextError);
        }

        if (!checker.IsDockerDaemonRunning())
        {
            return Fail(DaemonRow, "not reachable", "Docker connection unavailable",
                FailureDetails(checker, "Start Docker on this machine and check that your account has Docker access."));
        }

        Pass(DaemonRow, "running");
        Start(PlatformRow);

        var daemonPlatformError = checker.GetDockerPlatformError();

        if (!string.IsNullOrWhiteSpace(daemonPlatformError))
        {
            return Fail(PlatformRow, "unsupported", "Docker platform unsupported or unavailable", daemonPlatformError);
        }

        Pass(PlatformRow, "Linux amd64");
        Start(CpuRow, MemoryRow, DiskRow);

        var resources = checker.GetSystemResources();
        var features = shell.Context.Features;

        shell.Context.Resources = resources;
        AssessCapacity(rows, resources, features, calculator.CalculateMinimumRamMb(features));
        SidebarStats.Push(shell, calculator);

        var warnings = calculator.GetWarnings(resources, features);
        var shortOnMemory = rows[MemoryRow].State == CheckState.Failed;
        var finished = rows.ToArray();
        KeyHint[] hints = shortOnMemory
            ? [new KeyHint("Enter", "continue anyway"), new KeyHint("Esc", "back")]
            : [new KeyHint("Enter", "continue"), new KeyHint("Esc", "back")];

        shell.SetMain(() => Page(finished, FinishedFooter(resources, features, warnings, shortOnMemory)), hints);

        if (Widgets.WaitForEnter(shell))
        {
            return ScreenOutcome.Back;
        }

        return ScreenOutcome.Continue;
    }

    private static void AssessCapacity(CheckRow[] rows, SystemResources resources, FeatureSelection features, long neededMb)
    {
        var cores = ResourceCalculator.CpuLimit(resources);
        var recommendedCores = ResourceCalculator.RecommendedCpuCores(features);
        var memoryMb = ResourceCalculator.MemoryLimitMb(resources);
        var audioNote = recommendedCores == ResourceCalculator.AudioAnalysisCpuCores ? " with audio tagging" : string.Empty;

        rows[CpuRow] = rows[CpuRow] with
        {
            State = MachineFit.Cpu(cores, recommendedCores),
            Detail = WithSource($"{cores} cores", resources.DockerCpuCores.HasValue),
            Requirement = $"{recommendedCores} recommended{audioNote}",
        };

        var memoryState = MachineFit.Memory(memoryMb, neededMb);

        rows[MemoryRow] = rows[MemoryRow] with
        {
            State = memoryState,
            Detail = memoryState == CheckState.Unknown
                ? "not measured"
                : WithSource(Gigabytes(memoryMb), resources.DockerMemoryMb.HasValue),
            Requirement = $"selection needs about {Gigabytes(neededMb)}",
        };

        var diskState = MachineFit.Disk(resources.DockerAvailableDiskMb);

        if (diskState == CheckState.Unknown)
        {
            rows[DiskRow] = rows[DiskRow] with
            {
                State = diskState,
                Detail = "not measured",
                Requirement = resources.DiskSpaceDetails,
            };

            return;
        }

        var location = string.IsNullOrWhiteSpace(resources.DockerDataDirectory)
            ? string.Empty
            : $" · {resources.DockerDataDirectory}";

        rows[DiskRow] = rows[DiskRow] with
        {
            State = diskState,
            Detail = $"{Gigabytes(resources.DockerAvailableDiskMb.GetValueOrDefault())} free",
            Requirement = $"{Gigabytes(ResourceCalculator.MinimumDockerDiskMb)} needed{location}",
        };
    }

    // Everything below the capacity rows once the checks are done: where the
    // numbers came from, the memory bar, and the verdict
    private static IRenderable[] FinishedFooter(SystemResources resources, FeatureSelection features,
        IReadOnlyList<string> warnings, bool shortOnMemory)
    {
        var footer = new List<IRenderable>();

        if (!resources.DockerCpuCores.HasValue || !resources.DockerMemoryMb.HasValue)
        {
            footer.Add(Views.Note("Docker did not report its own limits, so this computer's totals are shown."));
        }

        var memoryMb = ResourceCalculator.MemoryLimitMb(resources);

        if (memoryMb > 0)
        {
            var segments = ServiceCatalog.Resolve(features).MemoryEstimates
                .Select(estimate => new MemorySegment
                {
                    Label = estimate.Label,
                    MemoryMb = estimate.MemoryMb,
                    Color = MemoryBudget.GroupColor(estimate.Label),
                })
                .ToList();
            var owner = resources.DockerMemoryMb.HasValue ? "Docker" : "this computer";

            footer.Add(Views.Blank());
            footer.Add(MemoryBudget.Build(segments, memoryMb, string.Empty, limitOwner: owner));
        }

        footer.Add(Views.Blank());

        if (warnings.Count == 0)
        {
            footer.Add(Views.SuccessPanel("Ready to continue",
                Markup.Escape("This machine meets the requirements for your selection.")));

            return [.. footer];
        }

        if (shortOnMemory)
        {
            footer.Add(Views.AttentionPanel("Not enough memory for this selection", warnings, severe: true));
            footer.Add(Views.Note("You can continue anyway, or go back and turn features off."));

            return [.. footer];
        }

        var title = warnings.Count == 1 ? "Ready, with one thing to know" : $"Ready, with {warnings.Count} things to know";

        footer.Add(Views.AttentionPanel(title, warnings, severe: false));

        return [.. footer];
    }

    private static IRenderable Page(CheckRow[] rows, IReadOnlyList<IRenderable> footer)
    {
        var parts = new List<IRenderable>
        {
            Views.Heading("Checking your machine",
                "Alexandria runs in Docker. These checks confirm it can build and start here."),
            Views.Section("Docker"),
            Views.CheckRows(rows.Take(DockerRowCount), LabelWidth),
            Views.Blank(),
            Views.Section("Room for your selection"),
            Views.CheckRows(rows.Skip(DockerRowCount), LabelWidth),
        };

        parts.AddRange(footer);

        return new Rows(parts);
    }

    private static ScreenOutcome? ShowFailure(Shell shell, CheckRow[] rows, string title, string body)
    {
        shell.SetMain(() => Page(rows,
            [
                Views.Blank(),
                Views.ErrorPanel(title, Markup.Escape(body)),
                Views.Note("Fix the issue, then check again. Nothing has been installed."),
            ]),
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

    private static string WithSource(string value, bool fromDocker) => fromDocker ? value : $"{value} (host)";

    private static string Gigabytes(long megabytes) => $"{megabytes / 1024.0:0.#} GB";

    // "Docker version 27.0.1, build abc" reads as just "27.0.1"; anything
    // without the usual shape is shown as reported
    internal static string ShortVersion(string versionLine)
    {
        const string marker = "version ";

        var line = versionLine.Trim();
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return line;
        }

        var version = line[(start + marker.Length)..].Split(',')[0].Trim();

        return version.Length == 0 ? line : version;
    }
}
