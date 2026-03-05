namespace Builder.Models;

public class PortMapping
{
    public string ServiceName { get; set; } = string.Empty;
    public string PortKey { get; set; } = string.Empty;
    public int DefaultPort { get; set; }
    public int AssignedPort { get; set; }
    public bool IsConflicted { get; set; }
    public string? ConflictProcess { get; set; }
}
