using Builder.Services;
using Builder.UI;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Workflow;

public class MainFlow
{
    private readonly ISystemChecker _systemChecker;
    private readonly ICredentialService _credentialService;
    private readonly IResourceCalculator _resourceCalculator;
    private readonly IPortResolver _portResolver;
    private readonly IDockerService _dockerService;
    private readonly IConfigurationService _configService;

    public InstallationContext Context { get; } = new();

    public MainFlow()
    {
        _systemChecker = new SystemChecker();
        _credentialService = new CredentialService();
        _resourceCalculator = new ResourceCalculator();
        _portResolver = new PortResolver();
        _dockerService = new DockerService();
        _configService = new ConfigurationService(new TemplateService("Local"));
    }

    public void ExecuteFlow()
    {
        var context = Context;

        using var shell = new Shell(context);

        shell.OnAbort = () => Cleanup();

        shell.RunAll(
        [
            ("Welcome", s => IntroScreen.Run(s, UiPreferencesStore.Save)),
            ("Features", s => FeaturesScreen.Run(s, _resourceCalculator)),
            ("System check", s => SystemCheckScreen.Run(s, _systemChecker, _resourceCalculator)),
            ("Ports", s => PortsScreen.Run(s, _portResolver, _systemChecker)),
            ("Your account", s => AccountScreen.Run(s)),
            ("Where to install", s => TargetDirScreen.Run(s)),
            ("Summary", s => SummaryScreen.Run(s, _credentialService, _resourceCalculator)),
            ("Installing", s => DeployScreen.Run(s, _configService, _dockerService)),
            ("Done", s => SuccessScreen.Run(s)),
        ]);
    }


    public void Cleanup()
    {
        var context = Context;

        if (!string.IsNullOrEmpty(context.InstallPath) && Directory.Exists(context.InstallPath))
        {
            AnsiConsole.MarkupLine($"\n[yellow]Cleaning up...[/]");
            _dockerService.ComposeDown(context.InstallPath);
        }
    }
}
