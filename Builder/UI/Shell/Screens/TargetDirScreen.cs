using Builder.UI;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class TargetDirScreen
{
    private static readonly string[] RepoMarkers = ["Backend", "Frontend"];

    // Test seam: pins repo detection so flows never depend on where tests run
    internal static Func<string?>? RepoRootOverride { get; set; }

    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;

        var repoRoot = RepoRootOverride is not null
            ? RepoRootOverride()
            : DetectRepoRoot();

        IRenderable Header(string note) => new Rows(
            Views.Title("Where should Alexandria live?"),
            Views.Note(note));

        if (repoRoot is null)
        {
            var source = Widgets.AskText(
                shell,
                Header("Could not find the Alexandria source code automatically."),
                new TextFieldSpec
                {
                    Label = "Folder containing the source (with Backend/ and Frontend/)",
                    Initial = Environment.CurrentDirectory,
                    Validator = v => IsSource(v)
                        ? null
                        : "That folder does not look like an Alexandria checkout",
                });

            if (shell.TakeEscape()) return ScreenOutcome.Back;

            context.SourceRoot = source;
            context.Config.SourceRoot = source;
        }
        else
        {
            shell.SetMain(new Rows(
                Header("Two locations, two jobs - here is how it splits."),
                Views.CheckRow("Source code", true, repoRoot),
                Views.Note("    Stays exactly where it is. Alexandria builds from here."),
                Views.CheckRow("Your files & settings", true, "(picked below)"),
                Views.Note("    Passwords, database and generated config land here.")));

            if (IntroScreen.WaitEnter(shell))
            {
                return ScreenOutcome.Back;
            }
        }

        var defaultTarget = repoRoot ?? DefaultSibling(context.SourceRoot);

        var target = Widgets.AskText(
            shell,
            Header("Settings and files will be placed here. Enter accepts the suggestion."),
            new TextFieldSpec
            {
                Label = "Installation folder",
                Initial = defaultTarget,
            });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        context.InstallPath = target;
        context.Config.InstallPath = target;

        if (string.IsNullOrWhiteSpace(context.SourceRoot))
        {
            context.SourceRoot = repoRoot!;
            context.Config.SourceRoot = repoRoot!;
        }

        Directory.CreateDirectory(target);

        var confirmation = new List<IRenderable>
        {
            Header("Folder ready"),
            Views.CheckRow("Setup folder", true, target),
        };

        if (!string.Equals(
                Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(context.SourceRoot).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            confirmation.Add(Views.CheckRow("Code built from", true, context.SourceRoot));
        }

        shell.SetMain(new Rows(confirmation.ToArray()));

        if (File.Exists(Path.Combine(target, ".env")))
        {
            shell.SetMain(new Rows(
                new Rows(confirmation.ToArray()),
                Views.WarningPanel(
                    "This folder already has an Alexandria configuration. " +
                    "Your existing settings are kept - only missing values will be filled in.")));

            IntroScreen.WaitEnter(shell);
        }

        return ScreenOutcome.Continue;
    }

    private static bool IsSource(string path)
    {
        return RepoMarkers.All(m =>
            Directory.Exists(Path.Combine(path, m)) || File.Exists(Path.Combine(path, m)));
    }

    private static string DefaultSibling(string sourceRoot)
    {
        var parent = Path.GetDirectoryName(sourceRoot);

        return parent is null ? sourceRoot : Path.Combine(parent, "alexandria");
    }

    private static string? DetectRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = start;

            for (var i = 0; i < 10 && dir is not null; i++)
            {
                if (IsSource(dir))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        return null;
    }
}
