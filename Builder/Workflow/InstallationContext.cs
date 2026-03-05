using Builder.Models;

namespace Builder.Workflow;

public class InstallationContext
{
    public InstallationConfig Config { get; set; } = new();
    public FeatureSelection Features { get; set; } = new();
    public SystemResources Resources { get; set; } = new();
    public List<PortMapping> PortMappings { get; set; } = [];
    public bool ShouldAbort { get; set; }
    public string InstallPath { get; set; } = string.Empty;

    public bool IsLocalPreview => Config.Environment == Models.Environment.LocalPreview;
}
