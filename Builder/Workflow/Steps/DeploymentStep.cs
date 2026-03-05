using System.Diagnostics;
using Builder.Services;
using Builder.UI;

namespace Builder.Workflow.Steps;

public class DeploymentStep : WizardStep
{
    private readonly IConfigurationService _configService;
    private readonly IDockerService _dockerService;

    public DeploymentStep(IConfigurationService configService, IDockerService dockerService)
    {
        _configService = configService;
        _dockerService = dockerService;
    }

    public override string Title => "Deploy";
    public override int StepNumber => 7;

    protected override void Run(InstallationContext context)
    {
        Components.Muted($"Deploying to {context.InstallPath}");
        Components.Spacer();

        // Generate config files
        RunTask("Generating configuration", () =>
        {
            _configService.WriteAllConfigFiles(context.Config, context.InstallPath);
            return true;
        }, context);
        if (context.ShouldAbort) return;

        // Clone repo (deployment only)
        if (!context.IsLocalPreview)
        {
            RunTask("Cloning repository", () => CloneRepository(context), context);
            if (context.ShouldAbort) return;

            RunTask("Preparing files", () =>
            {
                CopyConfigsToProject(context);
                return true;
            }, context);
            if (context.ShouldAbort) return;
        }
        else
        {
            // Local preview: project source is already the install path
            context.Config.ProjectSourcePath = context.InstallPath;
        }

        // Build
        Components.Muted("Building Docker images (this may take a while)...");
        var buildOk = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Theme.AccentStyle)
            .Start("  Building...", ctx =>
            {
                buildOk = _dockerService.ComposeBuild(context.InstallPath, line =>
                    ctx.Status($"  [{Theme.Di}]{TruncateLine(line).EscapeMarkup()}[/]"));
            });

        if (buildOk)
            Components.CheckItem("Images built", true);
        else
        {
            Components.CheckItem("Images built", false);
            HandleFailure("Build failed.", context);
            return;
        }

        // Start
        RunTask("Starting services", () => _dockerService.ComposeUp(context.InstallPath), context);
        if (context.ShouldAbort) return;

        // Health check
        Components.Muted("Waiting for health checks...");
        var healthy = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Theme.AccentStyle)
            .Start("  Health checks...", _ =>
            {
                healthy = _dockerService.WaitForHealthy(context.InstallPath, TimeSpan.FromMinutes(5));
            });

        if (healthy)
            Components.CheckItem("All services healthy", true);
        else
        {
            Components.CheckItem("Health checks", false, "timeout — services may still be starting");
            Components.WarningPanel($"Check status: cd {context.InstallPath} && docker compose ps");
        }

        Components.Spacer();
    }

    private void RunTask(string label, Func<bool> action, InstallationContext context)
    {
        var ok = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Theme.AccentStyle)
            .Start($"  {label}...", _ => { ok = action(); });

        if (ok)
            Components.CheckItem(label, true);
        else
        {
            Components.CheckItem(label, false);
            HandleFailure($"{label} failed.", context);
        }
    }

    private static bool CloneRepository(InstallationContext context)
    {
        var searchDir = AppContext.BaseDirectory;
        var markers = new[] { "Backend", "Frontend", "docker-compose.template.yml" };

        for (var i = 0; i < 5; i++)
        {
            if (searchDir == null) break;
            if (markers.All(m => Directory.Exists(Path.Combine(searchDir, m)) || File.Exists(Path.Combine(searchDir, m))))
            {
                context.Config.ProjectSourcePath = searchDir;
                return true;
            }
            searchDir = Directory.GetParent(searchDir)?.FullName;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone git@github.com:SpasMilenkov/Project-Alexandria.git \"{context.InstallPath}/source\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                context.Config.ProjectSourcePath = Path.Combine(context.InstallPath, "source");
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static void CopyConfigsToProject(InstallationContext context)
    {
        var sourcePath = context.Config.ProjectSourcePath;
        var installPath = context.InstallPath;

        if (!sourcePath.StartsWith(installPath))
        {
            foreach (var file in new[] { "docker-compose.yml", ".env", "garage.prod.toml", "prometheus.yml", "docker-compose.override.yml" })
            {
                var src = Path.Combine(installPath, file);
                var dst = Path.Combine(sourcePath, file);
                if (File.Exists(src))
                    File.Copy(src, dst, overwrite: true);
            }

            var backupsDir = Path.Combine(sourcePath, "backups");
            if (!Directory.Exists(backupsDir))
                Directory.CreateDirectory(backupsDir);

            context.InstallPath = sourcePath;
            context.Config.InstallPath = sourcePath;
        }
    }

    private static void HandleFailure(string message, InstallationContext context)
    {
        Components.Spacer();
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"  [{Theme.Er}]{message.EscapeMarkup()}[/]")
                .HighlightStyle(Theme.AccentStyle)
                .AddChoices("Show logs", "Abort"));

        if (choice == "Show logs")
        {
            var statuses = new DockerService().GetContainerStatuses(context.InstallPath);
            foreach (var status in statuses)
                AnsiConsole.MarkupLine($"    [{Theme.Di}]{status.EscapeMarkup()}[/]");
            Components.Spacer();
            Components.Muted($"Full logs: cd {context.InstallPath} && docker compose logs");
        }

        context.ShouldAbort = true;
    }

    private static string TruncateLine(string line)
        => line.Length > 80 ? line[..77] + "..." : line;
}
