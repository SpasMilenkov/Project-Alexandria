using Builder.UI;

namespace Builder.Workflow.Steps;

public class InstallPathStep : WizardStep
{
    public override string Title => "Install Path";
    public override int StepNumber => 6;

    protected override void Run(InstallationContext context)
    {
        if (context.IsLocalPreview)
        {
            // Auto-detect repo root
            var repoRoot = DetectRepoRoot();

            if (repoRoot != null)
            {
                Components.CheckItem("Repository detected", true, repoRoot);
                Components.Spacer();

                if (!AnsiConsole.Confirm($"  Use [{Theme.Ac}]{repoRoot.EscapeMarkup()}[/] as install path?", true))
                {
                    repoRoot = PromptPath(repoRoot);
                }

                context.InstallPath = repoRoot;
                context.Config.InstallPath = repoRoot;
                context.Config.ProjectSourcePath = repoRoot;
                return;
            }

            Components.WarningPanel("Could not detect project repository. Please specify the path.");
        }

        var defaultPath = context.IsLocalPreview
            ? (DetectRepoRoot() ?? System.Environment.CurrentDirectory)
            : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "alexandria");

        var path = PromptPath(defaultPath);

        context.InstallPath = path;
        context.Config.InstallPath = path;

        // Check for existing installation
        var configFile = Path.Combine(path, "alexandria-config.json");
        if (File.Exists(configFile))
        {
            Components.Spacer();
            Components.WarningPanel("Existing installation found. Configuration files will be overwritten.\nData volumes are preserved.");

            if (!AnsiConsole.Confirm($"  [{Theme.Wa}]Overwrite configuration?[/]", false))
            {
                context.ShouldAbort = true;
                return;
            }
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Components.CheckItem("Directory created", true, path);
        }
        else
        {
            Components.CheckItem("Directory exists", true, path);
        }

        Components.Spacer();
    }

    private static string PromptPath(string defaultValue)
    {
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>($"  Install directory [{Theme.Di}](default: {defaultValue.EscapeMarkup()})[/]:")
                .DefaultValue(defaultValue)
                .Validate(p =>
                {
                    try { Path.GetFullPath(p); return ValidationResult.Success(); }
                    catch { return ValidationResult.Error("Invalid path"); }
                }));

        return Path.GetFullPath(path);
    }

    private static string? DetectRepoRoot()
    {
        var searchDir = AppContext.BaseDirectory;
        var markers = new[] { "Backend", "Frontend", "docker-compose.template.yml" };

        for (var i = 0; i < 10; i++)
        {
            if (searchDir == null) break;
            if (markers.All(m => Directory.Exists(Path.Combine(searchDir, m)) || File.Exists(Path.Combine(searchDir, m))))
                return searchDir;
            searchDir = Directory.GetParent(searchDir)?.FullName;
        }

        return null;
    }
}
