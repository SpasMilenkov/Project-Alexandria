using Builder.Models;

namespace Builder.Workflow;

public class InstallationContext
{
    public InstallationConfig Config { get; set; } = new();
    public FeatureSelection Features { get; set; } = new();
    public SystemResources Resources { get; set; } = new();
    public List<PortMapping> PortMappings { get; set; } = [];
    public bool ShouldAbort { get; set; }
    // Directory receiving the generated artifacts (locked decision D13)
    public string InstallPath { get; set; } = string.Empty;
    // Repository checkout used as build context for the images
    public string SourceRoot { get; set; } = string.Empty;

    public bool UsingExternalTarget =>
        !string.IsNullOrWhiteSpace(InstallPath) &&
        !string.IsNullOrWhiteSpace(SourceRoot) &&
        !string.Equals(
            Path.GetFullPath(InstallPath).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(SourceRoot).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    // Filled after phase-1 boot when garage-init output has been merged
    public GarageCredentialSet? GarageCredentials { get; set; }

    // Admin login chosen by the user (D8); flows into .env only, never into
    // the persisted configuration file
    public AdminAccountInput AdminAccount { get; set; } = new();

    // Set once the user has visited the feature selection screen; gates the
    // sidebar's per-feature indicator block (D47)
    public bool FeaturesSelected { get; set; }
}
