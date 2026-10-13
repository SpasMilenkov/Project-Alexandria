using Builder.Models;

namespace Builder.Workflow;

public class InstallationContext
{
    public InstallationConfig Config { get; set; } = new()
    {
        Features = FeatureSelection.FromDefaults(),
    };

    public string LaunchDirectory { get; init; } = Path.GetFullPath(Environment.CurrentDirectory);
    public string? SourcePathDraft { get; set; }
    public string? TargetPathDraft { get; set; }
    public string? PublicHostDraft { get; set; }
    public int? OwnedHttpsPort { get; set; }
    public bool AdminUsernameCustomized { get; set; }
    public bool IsRepair { get; set; }
    public bool ExistingStackReady { get; set; }
    public string? ApprovedEnvironmentContent { get; set; }
    public bool DeploymentStarted { get; set; }
    public bool Completed { get; set; }
    public bool DeploymentVerified { get; set; }
    public bool AdminLoginVerified { get; set; }
    public string? DeploymentLogPath { get; set; }
    public string? InspectedTarget { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();

    public FeatureSelection Features
    {
        get => Config.Features;
        set => Config.Features = value;
    }
    public SystemResources Resources { get; set; } = new();
    public List<PortMapping> PortMappings { get; set; } = [];
    public bool ShouldAbort { get; set; }
    public string InstallPath
    {
        get => Config.InstallPath;
        set => Config.InstallPath = value;
    }

    public string SourceRoot
    {
        get => Config.SourceRoot;
        set => Config.SourceRoot = value;
    }

    public bool UsingExternalTarget =>
        !string.IsNullOrWhiteSpace(InstallPath) &&
        !string.IsNullOrWhiteSpace(SourceRoot) &&
        !PathResolver.PathsEqual(InstallPath, SourceRoot);

    // Filled after phase-1 boot when garage-init output has been merged
    public GarageCredentialSet? GarageCredentials { get; set; }

    // Admin login chosen by the user (D8); flows into .env only, never into
    // the persisted configuration file
    public AdminAccountInput AdminAccount { get; set; } = new();

    // Set once the user has visited the feature selection screen; gates the
    // sidebar's per-feature indicator block (D47)
    public bool FeaturesSelected { get; set; }
}
