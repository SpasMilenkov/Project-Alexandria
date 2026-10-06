using Builder.Services;
using Builder.Models;
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
        var runner = new ProcessRunner();
        _systemChecker = new SystemChecker(runner, Context.Cancellation.Token);
        _credentialService = new CredentialService();
        _resourceCalculator = new ResourceCalculator();
        _portResolver = new PortResolver();
        _dockerService = new DockerService(runner);
        _configService = new ConfigurationService(new TemplateService("Local"));
    }

    internal MainFlow(IDockerService dockerService) : this()
    {
        _dockerService = dockerService;
    }

    public void ExecuteFlow()
    {
        var context = Context;

        using var shell = new Shell(context);

        shell.OnAbort = () => Cleanup();

        shell.RunAll(
        [
            ("Welcome", s => IntroScreen.Run(s, UiPreferencesStore.Save)),
            ("Where to install", SelectTarget),
            ("Features", s => FeaturesScreen.Run(s, _resourceCalculator)),
            ("System check", s => SystemCheckScreen.Run(s, _systemChecker, _resourceCalculator)),
            ("File storage", StorageScreen.Run),
            ("Network access", s => PortsScreen.Run(s, _portResolver, _systemChecker)),
            ("HTTPS certificate", s => CertificateScreen.Run(s)),
            ("Audio models", ModelSourceScreen.Run),
            ("Your account", s => AccountScreen.Run(s)),
            ("Summary", ReviewConfiguration),
            ("Installing", s => DeployScreen.Run(s, _configService, _dockerService, _systemChecker)),
            ("Done", s => SuccessScreen.Run(s)),
        ]);
    }

    private ScreenOutcome SelectTarget(Shell shell)
    {
        while (true)
        {
            var outcome = TargetDirScreen.Run(shell);

            if (outcome != ScreenOutcome.Continue)
            {
                return outcome;
            }

            if (Context.InspectedTarget is not null && PathResolver.PathsEqual(Context.InspectedTarget, Context.InstallPath))
            {
                return outcome;
            }

            try
            {
                var inspection = InstallationConfigurationResolver.Inspect(Context);
                var source = Context.SourceRoot;
                var target = Context.InstallPath;

                Context.Config = inspection.SavedConfiguration ?? new InstallationConfig
                {
                    Features = Context.Features,
                };
                Context.SourceRoot = source;
                Context.InstallPath = target;
                Context.IsRepair = inspection.HasEnvironment;
                Context.OwnedHttpsPort = inspection.HasEnvironment
                    ? Context.Config.Ports.GetValueOrDefault("HTTPS_PORT", Context.Config.PublicEndpoint.HttpsPort)
                    : null;
                Context.InspectedTarget = target;

                if (string.IsNullOrEmpty(Context.Config.PublicEndpoint.Host) &&
                    Uri.TryCreate(inspection.ExistingEnvironment.GetValueOrDefault("BASE_URL"), UriKind.Absolute, out var existingOrigin) &&
                    PublicEndpoint.TryNormalizeHost(existingOrigin.Host, out var existingHost, out _))
                {
                    Context.Config.PublicEndpoint.Host = existingHost;
                    Context.Config.PublicEndpoint.HttpsPort = Context.OwnedHttpsPort ?? existingOrigin.Port;
                }

                if (inspection.HasEnvironment)
                {
                    Context.AdminAccount = new AdminAccountInput
                    {
                        Email = inspection.ExistingEnvironment.GetValueOrDefault("ADMIN_EMAIL", string.Empty),
                        Username = inspection.ExistingEnvironment.GetValueOrDefault("ADMIN_USERNAME", string.Empty),
                        Password = inspection.ExistingEnvironment.GetValueOrDefault("ADMIN_PASSWORD", string.Empty),
                    };
                    Context.AdminUsernameCustomized = true;
                }

                return outcome;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException or System.Text.Json.JsonException)
            {
                shell.SetMain(new Spectre.Console.Rows(
                    Views.ErrorPanel("Existing configuration needs attention", Spectre.Console.Markup.Escape(ex.Message)),
                    Views.Note("Nothing has been replaced. Enter chooses another folder; Esc goes back.")));

                if (IntroScreen.WaitEnter(shell)) return ScreenOutcome.Back;
            }
        }
    }

    private ScreenOutcome ReviewConfiguration(Shell shell)
    {
        try
        {
            var inspection = InstallationConfigurationResolver.Inspect(Context);
            LegacyInstallationIdentity? identity = null;

            if (inspection.IsLegacy)
            {
                identity = _dockerService.DiscoverLegacyIdentity(Context.InstallPath,
                    cancellationToken: Context.Cancellation.Token);
            }

            var config = InstallationConfigurationResolver.Resolve(Context, _credentialService, identity);
            new CertificateService().Validate(Context.InstallPath, config.PublicEndpoint.Host, config.Certificates);
            _dockerService.Configure(config, Context.Cancellation.Token);
            _dockerService.VerifyInstallationOwnership(Context.InstallPath, config);
            Context.ApprovedEnvironmentContent = inspection.ExistingEnvContent;
            Context.ExistingStackReady = false;

            if (inspection.HasEnvironment && File.Exists(Path.Combine(Context.InstallPath, "docker-compose.yml")))
            {
                try
                {
                    var expected = _dockerService.GetExpectedServices(Context.InstallPath);
                    var actual = _dockerService.GetServiceStatuses(Context.InstallPath);
                    Context.ExistingStackReady = ContainerReadiness.Evaluate(expected, actual, ["rabbitmq-config", "garage-init"]).Ready;
                }
                catch (InvalidOperationException)
                {
                    // An interrupted artifact write can leave old Compose unreadable; ownership was checked independently.
                    Context.ExistingStackReady = false;
                }
            }

            var sourceError = SourceAssetValidator.Validate(Context.SourceRoot, Context.Features);

            if (sourceError is not null) throw new InvalidOperationException(sourceError);

            return SummaryScreen.Run(shell, _resourceCalculator);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException or System.Text.Json.JsonException)
        {
            shell.SetMain(new Spectre.Console.Rows(
                Views.ErrorPanel("Setup cannot continue", Spectre.Console.Markup.Escape(ex.Message)),
                Views.Note("Existing services and settings are preserved. Enter or Esc returns to your choices.")));
            IntroScreen.WaitEnter(shell);

            return ScreenOutcome.Back;
        }
    }


    public void Cleanup()
    {
        Cancel();
    }

    public void Cancel()
    {
        Context.ShouldAbort = true;
        Context.Cancellation.Cancel();
    }
}
