using System.Text.Json;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

internal sealed class DockerOwnershipInspector(Func<IReadOnlyList<string>, ProcessResult> query)
{
    private static readonly string[] CoreVolumes =
        ["postgres_local_data", "rabbitmq_local_data", "garage_local_meta_data", "garage_local_storage_data"];
    private sealed record OwnedMount(string Name, string Destination);
    private sealed record OwnedContainer(string Id, Dictionary<string, string> Labels, IReadOnlyList<OwnedMount> Volumes);
    private sealed record OwnedVolume(string Name, Dictionary<string, string> Labels);
    private sealed record Snapshot(IReadOnlyList<OwnedContainer> Containers, IReadOnlyList<OwnedVolume> Volumes);

    public LegacyInstallationIdentity Discover(string target)
    {
        var snapshot = Read();
        var candidates = snapshot.Containers.Where(container => MatchesTarget(container.Labels, target)).ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No Docker container labels prove which project and data volumes belong to this legacy target. " +
                                                "Preserve its files and volumes; use a validated backup or an explicit adoption procedure before retrying.");
        }

        var projects = candidates.Select(container => Label(container.Labels, "com.docker.compose.project"))
            .Distinct(StringComparer.Ordinal).ToList();

        if (projects.Count != 1 || string.IsNullOrWhiteSpace(projects[0]))
        {
            throw new InvalidOperationException("The legacy target maps to missing or conflicting Docker project labels. Adoption stopped before changing files.");
        }

        var project = projects[0];
        EnsureContainers(snapshot.Containers, target, project);
        var volumes = MapVolumes(snapshot, project);

        foreach (var required in CoreVolumes)
        {
            if (!volumes.ContainsKey(required))
            {
                throw new InvalidOperationException($"The legacy {required} data volume could not be mapped safely. Adoption stopped to avoid attaching an empty store.");
            }
        }

        return new LegacyInstallationIdentity(project, volumes, PreserveFixedContainerNames: true);
    }

    public void Verify(string target, InstallationConfig config) => Verify(Read(), target, config);

    public IReadOnlyList<(string Id, string Service)> FindDisabledServiceContainers(string target, InstallationConfig config)
    {
        var snapshot = Read();
        Verify(snapshot, target, config);
        var enabledServices = ServiceCatalog.Resolve(config.Features).ServiceIds.ToHashSet(StringComparer.Ordinal);
        var disabled = new List<(string Id, string Service)>();

        foreach (var container in snapshot.Containers.Where(container => Label(container.Labels, "com.docker.compose.project") == config.ProjectName))
        {
            var service = Label(container.Labels, "com.docker.compose.service");

            if (enabledServices.Contains(service)) continue;

            if (container.Id.Length != 64 || !container.Id.All(Uri.IsHexDigit))
            {
                throw new InvalidOperationException("Docker did not report an exact container identity for a disabled service. " +
                                                    "Feature reconciliation stopped before removing any containers.");
            }

            disabled.Add((container.Id, service));
        }

        return disabled;
    }

    private static void Verify(Snapshot snapshot, string target, InstallationConfig config)
    {
        EnsureContainers(snapshot.Containers, target, config.ProjectName);
        EnsureManagedServices(snapshot.Containers, config.ProjectName);
        var actualVolumes = MapVolumes(snapshot, config.ProjectName);

        foreach (var (key, actualName) in actualVolumes)
        {
            var expectedName = config.VolumeNames.GetValueOrDefault(key, $"{config.ProjectName}_{key}");

            if (!string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The installed volume for {key} does not match saved ownership metadata. " +
                                                    "Repair stopped before changing files or data attachments.");
            }
        }

        foreach (var (key, expectedName) in config.VolumeNames)
        {
            if (!actualVolumes.TryGetValue(key, out var actualName) || actualName != expectedName)
            {
                throw new InvalidOperationException($"The saved {key} data volume is missing or no longer owned by this project. Restore or inspect it before repair.");
            }
        }

        if (config.Stage is "infrastructure-started" or "storage-ready" or "api-ready" or "services-started" or "services-ready" or "complete")
        {
            foreach (var key in CoreVolumes)
            {
                if (!actualVolumes.ContainsKey(key))
                {
                    throw new InvalidOperationException($"The established {key} data volume is missing. Restore or inspect it before repair; setup will not silently initialize an empty replacement.");
                }
            }
        }

        if (actualVolumes.ContainsKey("grafana_local_data"))
        {
            var environmentPath = Path.Combine(target, ".env");
            var established = File.Exists(environmentPath)
                ? EnvGenerator.Parse(File.ReadAllText(environmentPath))
                : new Dictionary<string, string>();
            var password = established.GetValueOrDefault("GRAFANA_PASSWORD", established.GetValueOrDefault("GRAFANA_ADMIN_PASSWORD", string.Empty));
            var metadataPath = Path.Combine(target, "alexandria-config.json");

            if (string.IsNullOrWhiteSpace(password) && File.Exists(metadataPath))
            {
                var saved = InstallationConfigurationResolver.ReadMetadataFile(metadataPath);
                password = saved.Credentials.GetValueOrDefault("GRAFANA_PASSWORD", saved.Credentials.GetValueOrDefault("GRAFANA_ADMIN_PASSWORD", string.Empty));
            }

            if (string.IsNullOrWhiteSpace(password) || password.Equals("placeholder", StringComparison.OrdinalIgnoreCase) ||
                !config.EnvironmentValues.TryGetValue("GRAFANA_PASSWORD", out var effective) || effective != password)
            {
                throw new InvalidOperationException("An existing Grafana data volume has no matching established password in this installation's environment. Recover its credentials before enabling monitoring; setup will not replace them with a generated password.");
            }
        }
    }

    private Snapshot Read()
    {
        var containerIds = Lines(query(["ps", "--all", "--quiet"]).StandardOutput).ToList();
        var containers = new List<OwnedContainer>();

        if (containerIds.Count > 0)
        {
            const string format = "{\"Id\":{{json .Id}},\"Labels\":{{json .Config.Labels}},\"Mounts\":{{json .Mounts}}}";
            var result = query(new[] { "inspect", "--format", format }.Concat(containerIds).ToArray());

            foreach (var element in DockerJson.Objects(result.StandardOutput))
            {
                var mounts = element.GetProperty("Mounts").EnumerateArray()
                    .Where(mount => DockerJson.Text(mount, "Type") == "volume")
                    .Select(mount => new OwnedMount(DockerJson.Text(mount, "Name"), DockerJson.Text(mount, "Destination"))).ToList();
                containers.Add(new OwnedContainer(DockerJson.Text(element, "Id"), Labels(element), mounts));
            }
        }

        var names = Lines(query(["volume", "ls", "--format", "{{.Name}}"]).StandardOutput).ToList();
        var volumes = new List<OwnedVolume>();

        if (names.Count > 0)
        {
            const string format = "{\"Name\":{{json .Name}},\"Labels\":{{json .Labels}}}";
            var result = query(new[] { "volume", "inspect", "--format", format }.Concat(names).ToArray());

            foreach (var element in DockerJson.Objects(result.StandardOutput))
            {
                volumes.Add(new OwnedVolume(DockerJson.Text(element, "Name"), Labels(element)));
            }
        }

        return new Snapshot(containers, volumes);
    }

    private static void EnsureContainers(IReadOnlyList<OwnedContainer> containers, string target, string project)
    {
        foreach (var container in containers)
        {
            var matchesTarget = MatchesTarget(container.Labels, target);
            var matchesProject = Label(container.Labels, "com.docker.compose.project") == project;

            if ((matchesTarget || matchesProject) &&
                (!matchesTarget || !matchesProject || !HasCompleteTargetLabels(container.Labels, target)))
            {
                throw new InvalidOperationException("Docker project labels conflict with this installation's target or saved identity. " +
                                                    "Repair stopped before changing files; inspect project ownership first.");
            }
        }
    }

    private static void EnsureManagedServices(IReadOnlyList<OwnedContainer> containers, string project)
    {
        var knownServices = ServiceCatalog.All.Select(service => service.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var container in containers.Where(container => Label(container.Labels, "com.docker.compose.project") == project))
        {
            if (!knownServices.Contains(Label(container.Labels, "com.docker.compose.service")))
            {
                throw new InvalidOperationException("This Docker project contains an unknown service container. " +
                                                    "Inspect it before changing features; setup will not remove arbitrary containers.");
            }

            if (Label(container.Labels, "com.docker.compose.oneoff").Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This Docker project contains a one-off container. " +
                                                    "Inspect it before changing features; setup will not remove manually started containers.");
            }
        }
    }

    private static Dictionary<string, string> MapVolumes(Snapshot snapshot, string project)
    {
        var volumes = new Dictionary<string, string>(StringComparer.Ordinal);
        var owned = snapshot.Volumes.Where(volume => Label(volume.Labels, "com.docker.compose.project") == project).ToList();

        foreach (var volume in owned)
        {
            var key = Label(volume.Labels, "com.docker.compose.volume");

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(volume.Name) || !volumes.TryAdd(key, volume.Name))
            {
                throw new InvalidOperationException("Docker data-volume labels are incomplete or ambiguous. Inspect ownership before repair.");
            }
        }

        foreach (var container in snapshot.Containers.Where(container => Label(container.Labels, "com.docker.compose.project") == project))
        {
            foreach (var mount in container.Volumes)
            {
                if (!owned.Any(volume => volume.Name == mount.Name))
                {
                    throw new InvalidOperationException("An installed container uses a volume without matching project ownership labels. " +
                                                        "Repair stopped to preserve its actual data attachment.");
                }
            }

            var attachments = Label(container.Labels, "com.docker.compose.service") switch
            {
                "postgres" => new[] { ("postgres_local_data", "/var/lib/postgresql/data") },
                "rabbitmq" => [("rabbitmq_local_data", "/var/lib/rabbitmq")],
                "garage" => [("garage_local_meta_data", "/var/lib/garage/meta"), ("garage_local_storage_data", "/var/lib/garage/data")],
                _ => [],
            };

            foreach (var (key, destination) in attachments)
            {
                var actual = container.Volumes.Where(mount => mount.Destination == destination).ToList();

                if (actual.Count != 1 || !volumes.TryGetValue(key, out var expected) || actual[0].Name != expected)
                {
                    throw new InvalidOperationException($"The actual {key} container attachment cannot be mapped safely to its labeled volume. " +
                                                        "Inspect the existing data mount before repair.");
                }
            }
        }

        return volumes;
    }

    private static bool MatchesTarget(Dictionary<string, string> labels, string target) =>
        SamePath(Label(labels, "com.docker.compose.project.working_dir"), target) || ConfigFileMatches(labels, target);

    private static bool HasCompleteTargetLabels(Dictionary<string, string> labels, string target) =>
        SamePath(Label(labels, "com.docker.compose.project.working_dir"), target) && ConfigFileMatches(labels, target);

    private static bool ConfigFileMatches(Dictionary<string, string> labels, string target)
    {
        var files = Label(labels, "com.docker.compose.project.config_files");
        var expected = Path.Combine(target, "docker-compose.yml");

        return SamePath(files, expected) || files.Split(',').Any(file => SamePath(file, expected));
    }

    private static bool SamePath(string candidate, string expected)
    {
        if (!Path.IsPathFullyQualified(candidate))
        {
            return false;
        }

        try
        {
            return PathResolver.PathsEqual(candidate, expected);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> Labels(JsonElement element)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        if (element.TryGetProperty("Labels", out var values) && values.ValueKind == JsonValueKind.Object)
        {
            foreach (var value in values.EnumerateObject())
            {
                labels[value.Name] = value.Value.GetString() ?? string.Empty;
            }
        }

        return labels;
    }

    private static string Label(Dictionary<string, string> labels, string key) => labels.GetValueOrDefault(key, string.Empty);

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
