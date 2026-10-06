using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public sealed class InstallationInspection
{
    public InstallationConfig? SavedConfiguration { get; init; }
    public Dictionary<string, string> ExistingEnvironment { get; init; } = new();
    public string? ExistingEnvContent { get; init; }
    public bool IsLegacy { get; init; }
    public bool HasEnvironment { get; init; }
    public bool HasMetadata { get; init; }
    public bool IsIncompleteGeneration { get; init; }
    public bool IsUncommittedFreshGeneration { get; init; }
}

public static class InstallationConfigurationResolver
{
    public const string GenerationMarkerName = ".alexandria-generation.json";

    private static readonly string[] RequiredExistingSecrets =
    [
        "DB_PASSWORD", "RABBITMQ_PASSWORD", "API_RABBITMQ_PASSWORD",
        "DOCUMENT_WORKER_RABBITMQ_PASSWORD", "MEDIA_WORKER_RABBITMQ_PASSWORD",
        "TRANSPILATION_WORKER_RABBITMQ_PASSWORD", "MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD",
        "LYRICS_WORKER_RABBITMQ_PASSWORD", "PLAYLIST_WORKER_RABBITMQ_PASSWORD",
        "ESSENTIA_WORKER_RABBITMQ_PASSWORD", "GARAGE_RPC_SECRET", "GARAGE_ADMIN_TOKEN",
        "GARAGE_S3_METRICS_TOKEN", "JWT_SECRET", "CSRF_SECRET", "POSTGRES_EXPORTER_PASSWORD",
    ];

    public static InstallationInspection Inspect(InstallationContext context)
    {
        var target = ResolvePath(context.InstallPath, context.LaunchDirectory);
        var pathError = PathResolver.ValidateTarget(target, probeWritable: false);

        if (pathError is not null) throw new InvalidOperationException(pathError);

        var metadataPath = Path.Combine(target, "alexandria-config.json");
        var markerPath = Path.Combine(target, GenerationMarkerName);
        var environmentPath = Path.Combine(target, ".env");
        var hasMetadata = File.Exists(metadataPath);
        var saved = hasMetadata ? ReadMetadataFile(metadataPath) : null;
        var isLegacy = saved?.SchemaVersion == 1;
        GenerationCheckpoint? checkpoint = null;

        if (File.Exists(markerPath))
        {
            try
            {
                checkpoint = JsonSerializer.Deserialize(File.ReadAllText(markerPath), AlexandriaJsonContext.Default.GenerationCheckpoint);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The installation generation marker is invalid. Restore it or inspect the incomplete artifact set before continuing.");
            }

            if (checkpoint is null || checkpoint.SchemaVersion != 1 ||
                checkpoint.Configuration is null || checkpoint.Configuration.SchemaVersion != 2 ||
                !IsValidProjectName(checkpoint.Configuration.ProjectName) ||
                string.IsNullOrWhiteSpace(checkpoint.GenerationId) ||
                checkpoint.Configuration.GenerationId != checkpoint.GenerationId ||
                checkpoint.Stage is not ("writing" or "complete") || checkpoint.ArtifactHashes is null ||
                checkpoint.Configuration.Features?.Enabled is null || checkpoint.Configuration.Ports is null ||
                checkpoint.Configuration.OriginalPorts is null || checkpoint.Configuration.VolumeNames is null ||
                checkpoint.Configuration.PublicEndpoint is null || checkpoint.Configuration.Certificates is null || checkpoint.Configuration.Storage is null ||
                !Path.IsPathFullyQualified(checkpoint.Configuration.InstallPath) ||
                !PathResolver.PathsEqual(target, checkpoint.Configuration.InstallPath))
            {
                throw new InvalidOperationException("The installation generation marker is unrecognized. Inspect this folder before changing its files.");
            }

            if (saved is not null && saved.SchemaVersion == 2 &&
                !string.Equals(saved.ProjectName, checkpoint.Configuration.ProjectName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The metadata and generation marker identify different Docker projects. Recover the matching metadata before continuing.");
            }

            if (saved is null || checkpoint.Stage == "writing" || saved.GenerationId != checkpoint.GenerationId)
            {
                if (isLegacy) checkpoint.Configuration.Credentials = new Dictionary<string, string>(saved!.Credentials);
                saved = checkpoint.Configuration;
            }
        }

        var hasEnvironment = File.Exists(environmentPath);

        if (saved is null && (hasEnvironment ||
            File.Exists(Path.Combine(target, "docker-compose.yml")) ||
            File.Exists(Path.Combine(target, "garage.toml"))))
        {
            throw new InvalidOperationException(
                "This folder contains deployment settings without recognized Alexandria metadata. " +
                "Restore alexandria-config.json and inspect its Docker project/volumes, or choose a different empty folder. Existing files were preserved.");
        }

        var environmentContent = hasEnvironment ? File.ReadAllText(environmentPath) : null;
        var values = environmentContent is null ? new Dictionary<string, string>() : EnvGenerator.Parse(environmentContent);
        NormalizeAliases(values, retainAliases: true);

        var incomplete = checkpoint is not null &&
            (checkpoint.Stage != "complete" || saved!.GenerationId != checkpoint.GenerationId ||
             !HashesMatch(target, checkpoint.ArtifactHashes));

        return new InstallationInspection
        {
            SavedConfiguration = saved,
            ExistingEnvironment = values,
            ExistingEnvContent = environmentContent,
            IsLegacy = isLegacy,
            HasEnvironment = hasEnvironment,
            HasMetadata = hasMetadata,
            IsIncompleteGeneration = incomplete,
            IsUncommittedFreshGeneration = checkpoint is { Stage: "writing", Configuration.Stage: "draft" } &&
                !hasMetadata && !hasEnvironment && checkpoint.ArtifactHashes.Count == 0,
        };
    }

    public static InstallationConfig Resolve(
        InstallationContext context,
        ICredentialService credentialService,
        LegacyInstallationIdentity? confirmedIdentity = null)
    {
        var inspection = Inspect(context);
        var draft = context.Config;
        var saved = inspection.SavedConfiguration;
        var target = ResolvePath(draft.InstallPath, context.LaunchDirectory);
        var source = ResolvePath(draft.SourceRoot, context.LaunchDirectory);

        if (inspection.IsLegacy && confirmedIdentity is null)
        {
            throw new InvalidOperationException(
                "Legacy Alexandria metadata was found. Confirm its existing Docker project and physical volume names before adopting it; no credentials or files have changed.");
        }

        if (inspection.IsLegacy && confirmedIdentity is not null)
        {
            foreach (var required in new[] { "postgres_local_data", "rabbitmq_local_data", "garage_local_meta_data", "garage_local_storage_data" })
            {
                if (!confirmedIdentity.VolumeNames.TryGetValue(required, out var physicalName) || string.IsNullOrWhiteSpace(physicalName))
                    throw new InvalidOperationException($"The confirmed legacy identity has no physical '{required}' volume. Inspect its existing data attachments before adoption.");
            }
        }

        var values = new Dictionary<string, string>(inspection.ExistingEnvironment);
        var recognizedRepair = saved is not null && !inspection.IsUncommittedFreshGeneration;
        var previousCredentials = new Dictionary<string, string>(saved?.Credentials ?? draft.Credentials);
        NormalizeAliases(previousCredentials);

        var endpoint = draft.PublicEndpoint;

        if (string.IsNullOrEmpty(endpoint.Host) && saved is not null) endpoint = saved.PublicEndpoint;

        endpoint.Validate();

        foreach (var (key, value) in previousCredentials)
        {
            if (values.TryGetValue(key, out var established) && established != value &&
                !IsPending(established) && !IsPending(value))
            {
                throw new InvalidOperationException($"Existing .env and legacy metadata disagree on '{key}'. Recover the established value before continuing; both copies were preserved.");
            }

            if (!values.ContainsKey(key)) values[key] = value;
        }

        var alreadyResolved = !recognizedRepair && draft.EnvironmentValues.Count > 0;

        if (alreadyResolved)
        {
            foreach (var (key, value) in draft.EnvironmentValues)
            {
                if (!values.ContainsKey(key)) values[key] = value;
            }
        }

        NormalizeAliases(values, retainAliases: true);
        if (inspection.IsLegacy && !inspection.HasEnvironment)
        {
            values.TryAdd("DB_NAME", "alexandria_dev");
            values.TryAdd("DB_USERNAME", "alexandria_user");
            values.TryAdd("DB_PORT", "5432");
        }

        if (recognizedRepair)
        {
            foreach (var key in RequiredExistingSecrets)
            {
                RequireEstablishedValue(values, key);
            }

            foreach (var key in new[]
            {
                "DB_NAME", "DB_USERNAME", "DB_PORT", "RABBITMQ_USER", "RABBITMQ_VHOST", "API_RABBITMQ_USER",
                "RABBITMQ_DOCUMENT_WORKER", "RABBITMQ_MEDIA_WORKER", "RABBITMQ_TRANSPILATION_WORKER",
                "RABBITMQ_MEDIA_METADATA_WORKER", "RABBITMQ_LYRICS_WORKER", "RABBITMQ_PLAYLIST_WORKER", "RABBITMQ_ESSENTIA_WORKER",
            })
            {
                RequireEstablishedValue(values, key);
            }

            if (draft.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
            {
                if (!saved!.Features.IsEnabled(FeatureCatalog.Monitoring.Id) &&
                    (!values.TryGetValue("GRAFANA_PASSWORD", out var grafanaPassword) || string.IsNullOrEmpty(grafanaPassword)))
                {
                    if (saved.VolumeNames.ContainsKey("grafana_local_data"))
                        throw new InvalidOperationException("The existing Grafana data volume has no established 'GRAFANA_PASSWORD'. Recover its credentials before enabling monitoring.");

                    if (draft.EnvironmentValues.TryGetValue("GRAFANA_PASSWORD", out var reviewedPassword) && !IsPending(reviewedPassword))
                    {
                        values["GRAFANA_PASSWORD"] = reviewedPassword;
                    }
                    else
                    {
                        values["GRAFANA_PASSWORD"] = credentialService.GeneratePassword();
                    }
                }

                RequireEstablishedValue(values, "GRAFANA_PASSWORD");
            }

            if (saved!.Stage is "storage-ready" or "api-ready" or "services-started" or "services-ready" or "complete")
            {
                foreach (var key in new[]
                {
                    "GARAGE_S3_ACCESS_KEY", "GARAGE_S3_SECRET_KEY", "GARAGE_S3_WORKER_ACCESS_KEY",
                    "GARAGE_S3_WORKER_SECRET_KEY", "GARAGE_S3_TRANSPILATION_WORKER_ACCESS_KEY", "GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY",
                })
                {
                    RequireEstablishedValue(values, key);
                }
            }
        }
        else if (!alreadyResolved)
        {
            foreach (var (key, value) in credentialService.GenerateAllCredentials(draft.Features))
            {
                previousCredentials[key] = value;
            }

            NormalizeAliases(previousCredentials);

            foreach (var (key, value) in previousCredentials)
            {
                values.TryAdd(key, value);
            }
        }

        if (!recognizedRepair && draft.Features.IsEnabled(FeatureCatalog.Monitoring.Id) &&
            (!values.TryGetValue("GRAFANA_PASSWORD", out var freshGrafanaPassword) || IsPending(freshGrafanaPassword)))
        {
            values["GRAFANA_PASSWORD"] = credentialService.GeneratePassword();
        }

        var defaults = EnvGenerator.BuildValues(draft.Features, values, context.AdminAccount, context.GarageCredentials);
        var storage = StorageConfigurationResolver.Resolve(draft.Storage, saved?.Storage, values, recognizedRepair);

        foreach (var (key, value) in defaults)
        {
            values.TryAdd(key, value);
        }

        StorageConfigurationResolver.ApplyEnvironment(storage, values);

        if (context.GarageCredentials is not null)
        {
            if (!context.GarageCredentials.IsComplete)
                throw new InvalidOperationException("The authoritative storage key set is incomplete. Preserve its handoff and rerun storage initialization before continuing.");

            foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(context.GarageCredentials))
            {
                if (values.TryGetValue(key, out var established) && !IsPending(established) && established != value)
                    throw new InvalidOperationException($"Authoritative storage keys disagree with established '{key}'. Recover the matching credentials before continuing; existing files were preserved.");

                values[key] = value;
            }
        }

        foreach (var key in ServiceCatalog.Resolve(draft.Features).EnvironmentFlags.Keys)
        {
            values[key] = defaults[key];
        }

        if (!recognizedRepair)
        {
            foreach (var key in new[] { "ADMIN_EMAIL", "ADMIN_USERNAME", "ADMIN_PASSWORD" })
            {
                values[key] = defaults[key];
            }
        }

        NormalizeAliases(values, retainAliases: true);
        ValidateGarageConfiguration(target, values);
        values["BASE_URL"] = endpoint.Origin;
        values["GARAGE_S3_PUBLIC_ENDPOINT"] = endpoint.Origin;
        values["CORS_ORIGIN"] = endpoint.Origin;
        values["CORS_ALLOWED_ORIGINS__0"] = endpoint.Origin;
        values["GRAFANA_ROOT_URL"] = endpoint.Origin + "/monitoring/";

        foreach (var key in values.Keys.Where(key => Regex.IsMatch(key, "^CORS_ALLOWED_ORIGINS__[0-9]+$") && key != "CORS_ALLOWED_ORIGINS__0").ToArray())
        {
            values.Remove(key);
        }

        values["DB_CONNECTION_STRING"] = EnvGenerator.BuildDatabaseConnectionString(values);
        values["ESSENTIA_RABBITMQ_URL"] = EnvGenerator.BuildAmqpUri(
            "rabbitmq", values.GetValueOrDefault("RABBITMQ_ESSENTIA_WORKER", "essentia_worker_user"),
            values["ESSENTIA_WORKER_RABBITMQ_PASSWORD"], values["RABBITMQ_VHOST"]);

        var projectName = saved?.ProjectName ?? draft.ProjectName;
        var preserveFixedNames = saved?.PreserveFixedContainerNames ?? draft.PreserveFixedContainerNames;
        var volumeNames = new Dictionary<string, string>(saved?.VolumeNames ?? draft.VolumeNames);

        if (confirmedIdentity is not null)
        {
            if (saved?.SchemaVersion == 2 && !string.Equals(projectName, confirmedIdentity.ProjectName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The inspected Docker project differs from the recorded project identity. Existing settings were preserved.");
            }

            projectName = confirmedIdentity.ProjectName;
            preserveFixedNames = confirmedIdentity.PreserveFixedContainerNames;
            volumeNames = new Dictionary<string, string>(confirmedIdentity.VolumeNames);
        }

        if (string.IsNullOrEmpty(projectName)) projectName = "alexandria-" + Guid.NewGuid().ToString("N")[..12];
        if (!IsValidProjectName(projectName)) throw new InvalidOperationException("The Docker project identity is invalid. Restore the installation's recorded project name.");

        values["COMPOSE_PROJECT_NAME"] = projectName;

        var installedAt = saved?.InstalledAt ?? draft.InstalledAt;
        if (installedAt == default) installedAt = DateTimeOffset.UtcNow;

        var effective = new InstallationConfig
        {
            SchemaVersion = 2,
            ProjectName = projectName,
            PreserveFixedContainerNames = preserveFixedNames,
            VolumeNames = volumeNames,
            PublicEndpoint = new PublicEndpoint { Host = endpoint.Host, HttpsPort = endpoint.HttpsPort },
            Certificates = draft.Certificates,
            Storage = storage,
            Features = draft.Features,
            ModelSourceDirectory = draft.ModelSourceDirectory,
            AllowModelDownloads = draft.AllowModelDownloads,
            Ports = new Dictionary<string, int> { ["HTTPS_PORT"] = endpoint.HttpsPort },
            OriginalPorts = endpoint.HttpsPort == 443 ? new() : new() { ["HTTPS_PORT"] = 443 },
            InstallPath = target,
            SourceRoot = source,
            SourceVersion = ReadSourceVersion(source),
            InstalledAt = installedAt,
            Stage = saved?.Stage ?? draft.Stage,
            GenerationId = saved?.GenerationId ?? draft.GenerationId,
            Credentials = new Dictionary<string, string>(values),
            EnvironmentValues = values,
        };

        if (string.IsNullOrEmpty(effective.GenerationId)) effective.GenerationId = Guid.NewGuid().ToString("N");
        context.Config = effective;

        return effective;
    }

    internal static InstallationConfig ReadMetadataFile(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Features", out var features) || features.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Ports", out var ports) || ports.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The existing metadata is not a recognized Alexandria installation. Its files were preserved.");
            }

            var config = JsonSerializer.Deserialize(root.GetRawText(), AlexandriaJsonContext.Default.InstallationConfig)
                ?? throw new InvalidOperationException("The installation metadata is empty.");
            if (root.TryGetProperty("SchemaVersion", out var schema))
            {
                if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var schemaVersion))
                    throw new InvalidOperationException("The installation metadata schema is invalid. Restore its original file before continuing.");

                config.SchemaVersion = schemaVersion;
            }
            else
            {
                config.SchemaVersion = 1;
            }

            if (config.SchemaVersion is not (1 or 2))
            {
                throw new InvalidOperationException("This installation metadata schema is unsupported. Use a compatible installer; existing files were preserved.");
            }

            if (config.Features?.Enabled is null || config.Ports is null || config.OriginalPorts is null || config.VolumeNames is null ||
                config.PublicEndpoint is null || config.Certificates is null || config.Storage is null)
            {
                throw new InvalidOperationException("The installation metadata has invalid settings collections. Restore its original file before continuing.");
            }

            if (config.SchemaVersion == 2 && !IsValidProjectName(config.ProjectName))
            {
                throw new InvalidOperationException("The installation metadata has no valid Docker project identity. Inspect its resources before continuing.");
            }

            if (config.SchemaVersion == 1 && root.TryGetProperty("Credentials", out var credentials))
            {
                if (credentials.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("Legacy credential metadata is malformed. Restore its original file before migration.");

                foreach (var property in credentials.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new InvalidOperationException($"Legacy credential '{property.Name}' is not a string. Restore the original metadata.");

                    config.Credentials[property.Name] = property.Value.GetString()!;
                }
            }

            return config;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new InvalidOperationException("The installation metadata is invalid or damaged. Restore the original metadata before continuing.");
        }
    }

    private static void RequireEstablishedValue(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || value.Equals("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Existing installation value '{key}' is missing. Restore it from the installation's protected .env/legacy metadata or recover it from the initialized service. A replacement credential will not be guessed.");
        }
    }

    private static void NormalizeAliases(Dictionary<string, string> values, bool retainAliases = false)
    {
        foreach (var (alias, canonical) in new[]
        {
            ("GARAGE_METRICS_TOKEN", "GARAGE_S3_METRICS_TOKEN"),
            ("GRAFANA_ADMIN_PASSWORD", "GRAFANA_PASSWORD"),
        })
        {
            if (!values.TryGetValue(alias, out var aliasValue)) continue;

            if (values.TryGetValue(canonical, out var canonicalValue) && aliasValue != canonicalValue)
            {
                throw new InvalidOperationException($"Existing aliases '{alias}' and '{canonical}' disagree. Recover their established value before rewriting the installation; both copies were preserved.");
            }

            values.TryAdd(canonical, aliasValue);
            if (!retainAliases) values.Remove(alias);
        }
    }

    private static bool IsPending(string value) => string.IsNullOrWhiteSpace(value) ||
        value.Equals("placeholder", StringComparison.OrdinalIgnoreCase);

    private static void ValidateGarageConfiguration(string target, IReadOnlyDictionary<string, string> values)
    {
        var path = Path.Combine(target, "garage.toml");
        if (!File.Exists(path)) return;
        var content = File.ReadAllText(path);

        foreach (var (tomlKey, environmentKey) in new[]
        {
            ("rpc_secret", "GARAGE_RPC_SECRET"),
            ("admin_token", "GARAGE_ADMIN_TOKEN"),
            ("metrics_token", "GARAGE_S3_METRICS_TOKEN"),
        })
        {
            var match = Regex.Match(content, @"(?m)^\s*" + tomlKey + """\s*=\s*(?<value>"(?:\\.|[^"\\])*")\s*(?:#.*)?$""");

            if (!match.Success)
                throw new InvalidOperationException($"Existing garage.toml has an unsupported or missing '{tomlKey}'. Inspect it before rewriting the installation.");

            string? existingValue;

            try
            {
                using var parsed = JsonDocument.Parse(match.Groups["value"].Value);
                existingValue = parsed.RootElement.GetString();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"Existing garage.toml uses unsupported escaping for '{tomlKey}'. Inspect it before rewriting the installation.");
            }

            if (!values.TryGetValue(environmentKey, out var effectiveValue) || existingValue != effectiveValue)
                throw new InvalidOperationException($"Existing garage.toml '{tomlKey}' conflicts with '{environmentKey}'. Recover the established Garage credentials before continuing.");
        }
    }

    private static bool HashesMatch(string target, Dictionary<string, string> hashes)
    {
        if (hashes.Count == 0) return false;

        foreach (var (relativePath, expected) in hashes)
        {
            var path = Path.GetFullPath(Path.Combine(target, relativePath));
            var relative = Path.GetRelativePath(target, path);

            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar)) return false;
            if (!File.Exists(path)) return false;
            using var file = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    internal static bool IsValidProjectName(string? name) =>
        !string.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[a-z0-9][a-z0-9_-]*$");

    private static string ResolvePath(string value, string launchDirectory)
    {
        if (!PathResolver.TryResolve(value, launchDirectory, out var path, out var error)) throw new InvalidOperationException(error);

        return path;
    }

    private static string ReadSourceVersion(string source)
    {
        try
        {
            var gitDirectory = Path.Combine(source, ".git");

            if (File.Exists(gitDirectory))
            {
                var link = File.ReadAllText(gitDirectory).Trim();
                if (!link.StartsWith("gitdir: ", StringComparison.Ordinal)) return "unknown";
                gitDirectory = Path.GetFullPath(link[8..], source);
            }

            var head = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(head)) return "unknown";
            var version = File.ReadAllText(head).Trim();

            if (version.StartsWith("ref: ", StringComparison.Ordinal))
            {
                var reference = Path.Combine(gitDirectory, version[5..]);
                if (!File.Exists(reference)) return "unknown";
                version = File.ReadAllText(reference).Trim();
            }

            return Regex.IsMatch(version, "^[0-9a-fA-F]{40,64}$") ? version : "unknown";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return "unknown";
        }
    }
}
