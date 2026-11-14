namespace Builder.Docker;

public sealed record LegacyInstallationIdentity(
    string ProjectName,
    Dictionary<string, string> VolumeNames,
    bool PreserveFixedContainerNames = true);
