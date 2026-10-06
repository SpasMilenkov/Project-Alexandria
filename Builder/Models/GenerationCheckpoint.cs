namespace Builder.Models;

public sealed class GenerationCheckpoint
{
    public int SchemaVersion { get; set; } = 1;
    public string GenerationId { get; set; } = string.Empty;
    public string Stage { get; set; } = "writing";
    public InstallationConfig Configuration { get; set; } = new();
    public Dictionary<string, string> ArtifactHashes { get; set; } = new();
}
