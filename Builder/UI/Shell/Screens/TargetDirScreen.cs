using Builder.UI;
using Builder.Workflow;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class TargetDirScreen
{
    internal static Func<string?>? RepoRootOverride { get; set; }

    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;
        var repoRoot = RepoRootOverride is not null
            ? RepoRootOverride()
            : DetectRepoRoot(context.LaunchDirectory);

        IRenderable Header(string note) => new Rows(
            Views.Title("Where should Alexandria live?"),
            Views.Note(note));

        var sourceInitial = context.SourcePathDraft ?? context.SourceRoot;

        if (context.SourcePathDraft is null && string.IsNullOrEmpty(sourceInitial))
        {
            sourceInitial = repoRoot ?? context.LaunchDirectory;
        }

        var source = Widgets.AskText(
            shell,
            Header("Source code stays here. Alexandria builds from here."),
            new TextFieldSpec
            {
                Label = "Alexandria source folder",
                Initial = sourceInitial,
                Hint = "Relative paths resolve from the folder where setup was launched.",
                Validator = value => ResolveSource(value, context),
            });

        context.SourcePathDraft = source;

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        PathResolver.TryResolve(source, context.LaunchDirectory, out var resolvedSource, out _);
        context.SourceRoot = resolvedSource;

        var targetInitial = context.TargetPathDraft ?? context.InstallPath;

        if (context.TargetPathDraft is null && string.IsNullOrEmpty(targetInitial))
        {
            targetInitial = repoRoot ?? DefaultSibling(resolvedSource);
        }

        string? writeError = null;

        while (true)
        {
            IRenderable targetHeader = Header(
                "This folder holds setup settings. Database and uploaded files use Docker-managed volumes.");

            if (writeError is not null)
            {
                targetHeader = new Rows(targetHeader, Views.WarningPanel(writeError));
            }

            var target = Widgets.AskText(
                shell,
                targetHeader,
                new TextFieldSpec
                {
                    Label = "Installation settings folder",
                    Initial = targetInitial,
                    Validator = value => ResolveTarget(value, context),
                });

            context.TargetPathDraft = target;

            if (shell.TakeEscape()) return ScreenOutcome.Back;

            PathResolver.TryResolve(target, context.LaunchDirectory, out var resolvedTarget, out _);

            var confirmation = new List<IRenderable>
            {
                Header("Review the resolved folders. Enter continues; Esc goes back without creating files."),
                Views.CheckRow("Source code", true, resolvedSource),
                Views.CheckRow("Setup settings", true, resolvedTarget),
                Views.Note("Database and uploaded files live in Docker-managed volumes, outside this settings folder."),
            };

            if (File.Exists(Path.Combine(resolvedTarget, ".env")))
            {
                confirmation.Add(Views.WarningPanel(
                    "This folder already has an Alexandria configuration. " +
                    "It must be inspected before deployment; selecting it does not overwrite settings."));
            }

            shell.SetMain(new Rows(confirmation.ToArray()));

            if (IntroScreen.WaitEnter(shell)) return ScreenOutcome.Back;

            writeError = PathResolver.ValidateTarget(resolvedTarget);

            if (writeError is null)
            {
                context.InstallPath = resolvedTarget;

                return ScreenOutcome.Continue;
            }

            targetInitial = target;
        }
    }

    private static string? ResolveSource(string value, InstallationContext context)
    {
        if (!PathResolver.TryResolve(value, context.LaunchDirectory, out var source, out var error))
        {
            return error;
        }

        return SourceAssetValidator.Validate(source, context.Features);
    }

    private static string? ResolveTarget(string value, InstallationContext context)
    {
        if (!PathResolver.TryResolve(value, context.LaunchDirectory, out var target, out var error))
        {
            return error;
        }

        return PathResolver.ValidateTarget(target, probeWritable: false);
    }

    private static string DefaultSibling(string sourceRoot)
    {
        var parent = Path.GetDirectoryName(sourceRoot);

        return parent is null ? sourceRoot : Path.Combine(parent, "alexandria");
    }

    private static string? DetectRepoRoot(string launchDirectory)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, launchDirectory })
        {
            var directory = new DirectoryInfo(start);

            for (var depth = 0; depth < 10 && directory is not null; depth++)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Backend", "Alexandria.Api", "Dockerfile")) &&
                    File.Exists(Path.Combine(directory.FullName, "Frontend", "Dockerfile")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
