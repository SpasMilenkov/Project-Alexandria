using Builder.Services;
using Builder.UI;

namespace Builder.Workflow.Steps;

public class SystemCheckStep : WizardStep
{
    private readonly ISystemChecker _systemChecker;
    private readonly IResourceCalculator _resourceCalculator;

    public SystemCheckStep(ISystemChecker systemChecker, IResourceCalculator resourceCalculator)
    {
        _systemChecker = systemChecker;
        _resourceCalculator = resourceCalculator;
    }

    public override string Title => "System Check";
    public override int StepNumber => 3;

    protected override void Run(InstallationContext context)
    {
        Components.SectionTitle("Runtime Dependencies");

        // OS
        var os = _systemChecker.GetOperatingSystem();
        Components.CheckItem("Operating System", true, os.ToString());

        // Docker
        if (!_systemChecker.IsDockerInstalled())
        {
            Components.CheckItem("Docker", false, "not installed");
            Components.Spacer();
            var (command, url) = _systemChecker.GetDockerInstallInstructions();
            Components.ErrorPanel(
                $"Docker is required.\n\nInstall: [bold white]{command}[/]\nDocs:    {url}");
            context.ShouldAbort = true;
            return;
        }
        Components.CheckItem("Docker", true, _systemChecker.GetDockerVersion());

        // Docker daemon
        if (!_systemChecker.IsDockerDaemonRunning())
        {
            Components.CheckItem("Docker Daemon", false, "not running");
            Components.Spacer();
            Components.ErrorPanel(
                "Docker daemon is not running.\n\n" +
                "sudo systemctl start docker[/]   Linux\n" +
                " open -a Docker[/]                macOS");
            context.ShouldAbort = true;
            return;
        }
        Components.CheckItem("Docker Daemon", true, "running");

        // Docker Compose
        if (!_systemChecker.IsDockerComposeInstalled())
        {
            Components.CheckItem("Docker Compose", false, "not installed");
            Components.Spacer();
            Components.ErrorPanel("Docker Compose is required. It ships with Docker Desktop.");
            context.ShouldAbort = true;
            return;
        }
        Components.CheckItem("Docker Compose", true, _systemChecker.GetDockerComposeVersion());

        // Git — only needed for deployment (not local preview)
        if (!context.IsLocalPreview)
        {
            if (!_systemChecker.IsGitInstalled())
            {
                Components.CheckItem("Git", false, "not installed");
                Components.Spacer();
                Components.ErrorPanel(
                    "Git is required to clone the repository.\n\n" +
                    "  [bold white]sudo apt install git[/]   Debian/Ubuntu\n" +
                    "  [bold white]brew install git[/]       macOS");
                context.ShouldAbort = true;
                return;
            }
            Components.CheckItem("Git", true, _systemChecker.GetGitVersion());
        }

        // System resources
        Components.SectionTitle("System Resources");

        context.Resources = _systemChecker.GetSystemResources();

        Components.CheckItem("CPU", context.Resources.CpuCores >= 2,
            $"{context.Resources.CpuCores} cores");
        Components.CheckItem("Memory", context.Resources.TotalMemoryMb >= 2048,
            $"{context.Resources.TotalMemoryMb} MB");

        if (context.Resources.AvailableDiskMb > 0)
        {
            Components.CheckItem("Disk", context.Resources.AvailableDiskMb >= 10240,
                $"{context.Resources.AvailableDiskMb / 1024.0:F1} GB free");
        }

        // Resource warnings
        var warnings = _resourceCalculator.GetWarnings(context.Resources, context.Features);
        if (warnings.Count > 0)
        {
            Components.Spacer();
            foreach (var warning in warnings)
                Components.WarningPanel(warning);

            // Only hard-block on deployment, not local preview
            if (!context.IsLocalPreview)
            {
                var minimumRam = _resourceCalculator.CalculateMinimumRamMb(context.Features);
                if (context.Resources.TotalMemoryMb > 0 && context.Resources.TotalMemoryMb < minimumRam * 0.5)
                {
                    if (!AnsiConsole.Confirm(
                            $"  [{Theme.Wa}]System is well below minimum requirements. Continue?[/]", false))
                    {
                        context.ShouldAbort = true;
                        return;
                    }
                }
            }
        }

        Components.Spacer();
    }
}
