namespace Builder.Models;

public sealed record LegacyInstallationIdentity(
    string ProjectName,
    Dictionary<string, string> VolumeNames,
    bool PreserveFixedContainerNames = true);
