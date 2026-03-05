namespace Builder.Models;

public class FeatureSelection
{
    public bool MediaProcessing { get; set; }
    public bool Monitoring { get; set; }
    public int FrontendPort { get; set; } = 80;
    public int GrafanaPort { get; set; } = 3000;

    // Debug ports (Local Preview only)
    public int PostgresPort { get; set; } = 5432;
    public int RabbitmqManagementPort { get; set; } = 15672;
}
