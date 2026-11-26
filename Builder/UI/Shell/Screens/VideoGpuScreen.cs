using Builder.Diagnostics;
using Builder.Docker;
using Builder.SystemCheck;
using Builder.UI.Shell.Input;
using Builder.UI.Shell.Screens.Components;
using Builder.UI.Theming;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// The GPU check opened from the entry screen. It changes no installation: picking a GPU only
// tells the setup run that follows which one to try first.
public static class VideoGpuScreen
{
    private const string Keep = "keep";
    private const string CheckAgain = "again";
    private const string Title = "Video GPU check";
    private const int BuildLogRows = 12;

    public static void Run(Shell shell, ISystemChecker checker, IDockerService docker)
    {
        var context = shell.Context;
        var initial = context.TargetPathDraft ?? context.InstallPath;

        if (string.IsNullOrEmpty(initial))
        {
            initial = InstallerArguments.DefaultSettingsFolder(context);
        }

        var input = Widgets.AskText(
            shell,
            Views.Heading(Title, "Tests which GPUs the transcoding worker can encode video on. No installation is changed."),
            new TextFieldSpec
            {
                Label = "Installation settings folder",
                Initial = initial,
                Validator = value => Validate(value, context),
            });

        if (shell.TakeEscape()) return;

        PathResolver.TryResolve(input, context.LaunchDirectory, out var folder, out _);

        var diagnosis = new VideoGpuDiagnosis(checker, docker);
        var next = VideoGpuNextStep.CheckAgain;

        while (next != VideoGpuNextStep.Leave)
        {
            var report = Diagnose(shell, diagnosis, folder, next == VideoGpuNextStep.BuildAndTest);
            next = ShowReport(shell, report);
        }
    }

    private static VideoGpuReport Diagnose(Shell shell, VideoGpuDiagnosis diagnosis, string folder, bool build)
    {
        var context = shell.Context;
        var recent = new Queue<string>();
        var gate = new object();

        void Record(string line)
        {
            lock (gate)
            {
                recent.Enqueue(line);

                if (recent.Count > BuildLogRows) recent.Dequeue();
            }
        }

        // The frame timer rebuilds this page off the screen's thread while the check runs
        shell.SetMain(() =>
        {
            string[] lines;

            lock (gate) lines = [.. recent];

            return Progress(folder, lines);
        }, [], animated: true);

        var checkout = PathResolver.FindSourceRoot(context.LaunchDirectory);

        return shell.RunResponsive(() => diagnosis.Run(folder, checkout, build, Record, context.Cancellation.Token));
    }

    private static IRenderable Progress(string folder, IReadOnlyList<string> buildLines)
    {
        var parts = new List<IRenderable>
        {
            Views.Heading(Title, folder),
            new Markup($"[bold {Theme.Active.Ac}]{Views.SpinnerFrame()}[/] Testing the GPUs…"),
            Views.Note("Each test starts a container, so this can take a minute."),
        };

        if (buildLines.Count > 0)
        {
            var width = Shell.MainContentWidth;

            parts.Add(Views.Blank());
            parts.AddRange(buildLines.Select(line => Views.Note(line.Length <= width ? line : line[..(width - 1)] + "…")));
        }

        return new Rows(parts);
    }

    private static VideoGpuNextStep ShowReport(Shell shell, VideoGpuReport report)
    {
        var usable = report.Nodes.Where(node => node.Encodes == true).ToArray();

        // With one usable GPU there is nothing to choose between
        if (usable.Length > 1) return ChooseGpu(shell, report, usable);

        var offersBuild = report.BuildSource is not null;
        KeyHint[] hints = offersBuild
            ? [new KeyHint("Enter", "build and test"), new KeyHint("R", "check again"), new KeyHint("Esc", "back")]
            : [new KeyHint("R", "check again"), new KeyHint("Enter", "back")];

        shell.SetMain(() => Page(report), hints, readFromTop: !offersBuild);

        while (true)
        {
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key)) continue;

            if (key.Key == ConsoleKey.R) return VideoGpuNextStep.CheckAgain;

            if (key.Key == ConsoleKey.Escape) return VideoGpuNextStep.Leave;

            if (key.Key != ConsoleKey.Enter) continue;

            return offersBuild ? VideoGpuNextStep.BuildAndTest : VideoGpuNextStep.Leave;
        }
    }

    private static VideoGpuNextStep ChooseGpu(Shell shell, VideoGpuReport report, IReadOnlyList<VideoGpuNodeReport> usable)
    {
        string[] choices = [Keep, .. usable.Select(node => node.Node.Path)];

        var choice = Widgets.AskSingleSelect(
            shell,
            Page(report),
            "Which GPU should the next setup run use?",
            choices,
            value => Label(value, usable),
            shortcut: (ConsoleKey.R, "check again", CheckAgain));

        if (choice is null)
        {
            shell.TakeEscape();

            return VideoGpuNextStep.Leave;
        }

        if (choice == CheckAgain) return VideoGpuNextStep.CheckAgain;

        if (choice == Keep) return VideoGpuNextStep.Leave;

        shell.Context.ChosenVideoGpu = choice;
        shell.Context.TargetPathDraft = report.Folder;
        var picked = usable.First(node => node.Node.Path == choice).Node;

        shell.SetMain(new Rows(
                Views.Heading(Title),
                Views.InfoPanel("Not applied yet", Markup.Escape(
                    $"Setup will try the {picked.Description} first. Choose \"Set up Alexandria on this machine\" " +
                    $"with the settings folder {report.Folder} to apply it."))),
            [new KeyHint("Enter", "back")]);
        Widgets.WaitForEnter(shell);
        shell.TakeEscape();

        return VideoGpuNextStep.Leave;
    }

    private static string Label(string choice, IReadOnlyList<VideoGpuNodeReport> usable)
    {
        if (choice == Keep) return "Leave it as it is";

        var node = usable.First(found => found.Node.Path == choice);
        var use = node.InUse ? " (in use)" : string.Empty;

        return node.Node.Description + use;
    }

    private static IRenderable Page(VideoGpuReport report)
    {
        var parts = new List<IRenderable> { Views.Heading(Title, report.Folder) };

        if (report.Setting is not null)
        {
            var facts = new List<(string, string)> { ("Installation", report.Setting) };

            if (report.WorkerDetail is not null || report.WorkerEncodes is not null)
            {
                facts.Add(("Worker as installed", VideoGpuReportText.Outcome(report.WorkerEncodes, report.WorkerDetail)));
            }

            parts.Add(Views.Facts(facts));
            parts.Add(Views.Blank());
        }

        if (report.Nodes.Count > 0)
        {
            parts.Add(Views.Section("Render nodes"));
            parts.Add(Nodes(report.Nodes));
            parts.Add(Views.Blank());
        }

        if (report.Notes.Count > 0)
        {
            parts.Add(Views.AttentionPanel("Things to know", report.Notes, report.WiredGpuFails || report.BuildFailed));
        }

        if (report.BuildSource is not null)
        {
            parts.Add(Views.InfoPanel("Build a worker image to test with?", Markup.Escape(
                $"It is compiled from {report.BuildSource} and removed after the test. The first build " +
                "needs network access and can take several minutes.")));
        }

        return new Rows(parts);
    }

    private static IRenderable Nodes(IReadOnlyList<VideoGpuNodeReport> nodes)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1))
            .AddColumn();

        foreach (var node in nodes)
        {
            var use = node.InUse ? " (in use)" : string.Empty;

            grid.AddRow(new Markup(Icon(node.Encodes)), new Markup(Markup.Escape(node.Node.Description + use)));
            grid.AddRow(Views.Blank(), Views.Note(node.Node.Path));
            grid.AddRow(Views.Blank(), new Markup(Markup.Escape(VideoGpuReportText.Outcome(node.Encodes, node.Detail))));
        }

        return grid;
    }

    private static string Icon(bool? encodes) => encodes switch
    {
        true => Ui.Success("✓"),
        false => Ui.Error("✗"),
        null => Ui.Muted("?"),
    };

    private static string? Validate(string value, InstallationContext context)
    {
        if (!PathResolver.TryResolve(value, context.LaunchDirectory, out var folder, out var error))
        {
            return error;
        }

        return PathResolver.ValidateTarget(folder, probeWritable: false);
    }
}
