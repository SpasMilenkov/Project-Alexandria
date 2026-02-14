namespace Builder.Models;

public enum Environment
{
    Development,
    LocalPreview,
    Deployment
}

public enum S3Provider
{
    Garage,
    MinIO,
    RustFS
}

public class InstallationConfig
{
    public Environment Environment { get; set; }
    public S3Provider S3Provider { get; set; }
    public Dictionary<string, int> Ports { get; set; } = new();
    public Dictionary<string, string> Credentials { get; set; } = new();
    public string InstallPath { get; set; }
}

public class PortCheckResult
{
    public int Port { get; set; }
    public bool IsAvailable { get; set; }
    public string ServiceName { get; set; }
    public string ProcessBlocking { get; set; }
}