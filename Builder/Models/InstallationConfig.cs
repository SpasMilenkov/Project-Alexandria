namespace Builder.Models;

public class InstallationConfig
{
    public FeatureSelection Features { get; set; } = new();
    public Dictionary<string, int> Ports { get; set; } = new();
    public Dictionary<string, int> OriginalPorts { get; set; } = new();
    public Dictionary<string, string> Credentials { get; set; } = new();
    // Where the generated stack lives (docker-compose.yml, .env, garage.toml)
    public string InstallPath { get; set; } = string.Empty;
    // The repository checkout the images are built from; equals InstallPath
    // for in-repo setups, absolute path reference otherwise (locked decision D13)
    public string SourceRoot { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; }
}
