using Builder.Services;
using Builder.UI;
using Builder.Workflow.Steps;
using Environment = Builder.Models.Environment;

namespace Builder.Workflow;

public class MainFlow
{
    private readonly ISystemChecker _systemChecker;
    private readonly ICredentialService _credentialService;
    private readonly IResourceCalculator _resourceCalculator;
    private readonly IPortResolver _portResolver;
    private readonly IDockerService _dockerService;
    private readonly InstallationContext _context;

    public MainFlow()
    {
        _systemChecker = new SystemChecker();
        _credentialService = new CredentialService();
        _resourceCalculator = new ResourceCalculator();
        _portResolver = new PortResolver();
        _dockerService = new DockerService();
        _context = new InstallationContext();
    }

    public void ExecuteFlow()
    {
        // WelcomeStep always runs first to determine environment
        var welcome = new WelcomeStep();
        welcome.Execute(_context);

        if (_context.ShouldAbort)
        {
            ExitMessage();
            return;
        }

        // Build remaining steps based on chosen environment
        var steps = BuildSteps(_context.Config.Environment);

        foreach (var step in steps)
        {
            step.Execute(_context);

            if (_context.ShouldAbort)
            {
                ExitMessage();
                return;
            }
        }
    }

    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(_context.InstallPath) && Directory.Exists(_context.InstallPath))
        {
            AnsiConsole.MarkupLine($"\n[{Theme.Wa}]Cleaning up...[/]");
            _dockerService.ComposeDown(_context.InstallPath);
        }
    }

    private static void ExitMessage()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{Theme.Di}]Exiting Alexandria Installer.[/]");
    }

    private List<WizardStep> BuildSteps(Environment env)
    {
        var templateService = new TemplateService("Production");
        var configService = new ConfigurationService(templateService);

        return env switch
        {
            Environment.Deployment => BuildDeploymentSteps(configService),
            Environment.LocalPreview => BuildLocalPreviewSteps(configService),
            _ => []
        };
    }

    private List<WizardStep> BuildDeploymentSteps(IConfigurationService configService)
    {
        return
        [
            new FeatureSelectionStep(),
            new SystemCheckStep(_systemChecker, _resourceCalculator),
            new PortResolutionStep(_portResolver, _systemChecker),
            new ConfigSummaryStep(_credentialService, _resourceCalculator),
            new InstallPathStep(),
            new DeploymentStep(configService, _dockerService),
            new SuccessStep(),
        ];
    }

    private List<WizardStep> BuildLocalPreviewSteps(IConfigurationService configService)
    {
        return
        [
            new FeatureSelectionStep(),
            new SystemCheckStep(_systemChecker, _resourceCalculator),
            new PortResolutionStep(_portResolver, _systemChecker),
            new ConfigSummaryStep(_credentialService, _resourceCalculator),
            new InstallPathStep(),
            new DeploymentStep(configService, _dockerService),
            new SuccessStep(),
        ];
    }
}
