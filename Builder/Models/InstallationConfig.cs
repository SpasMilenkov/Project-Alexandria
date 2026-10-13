using System.Text.Json.Serialization;

namespace Builder.Models;

public class InstallationConfig
{
    public int SchemaVersion { get; set; } = 2;
    public string ProjectName { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = "unknown";
    public string Stage { get; set; } = "draft";
    public string GenerationId { get; set; } = string.Empty;
    public bool PreserveFixedContainerNames { get; set; }
    public Dictionary<string, string> VolumeNames { get; set; } = new();
    public PublicEndpoint PublicEndpoint { get; set; } = new();
    public CertificateSettings Certificates { get; set; } = new();
    public StorageSettings Storage { get; set; } = new();
    public string? ModelSourceDirectory { get; set; }
    public bool AllowModelDownloads { get; set; }
    public FeatureSelection Features { get; set; } = new();
    public Dictionary<string, int> Ports { get; set; } = new();
    public Dictionary<string, int> OriginalPorts { get; set; } = new();
    [JsonIgnore]
    public Dictionary<string, string> Credentials { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, string> EnvironmentValues { get; set; } = new();
    // Where the generated stack lives (docker-compose.yml, .env, garage.toml)
    public string InstallPath { get; set; } = string.Empty;
    // The repository checkout used as the read-only build source.
    public string SourceRoot { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; }
}
