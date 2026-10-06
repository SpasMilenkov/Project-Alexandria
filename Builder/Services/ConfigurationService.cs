using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public interface IConfigurationService
{
    InstallationConfig CreateConfiguration(FeatureSelection features, Dictionary<string, string> credentials, Dictionary<string, int> ports);

    string GenerateDockerCompose(InstallationConfig config);

    string GenerateGarageToml(InstallationConfig config);

    string GeneratePrometheusYml(InstallationConfig? config = null);

    void BeginGeneration(InstallationConfig config, string outputPath);

    void WriteAllConfigFiles(InstallationConfig config, string outputPath);

    void SaveConfiguration(InstallationConfig config, string path);

    InstallationConfig? LoadConfiguration(string path);
}

public class ConfigurationService : IConfigurationService
{
    private readonly ITemplateService _templateService;
    private readonly string _launchDirectory;

    public ConfigurationService(ITemplateService templateService, string? launchDirectory = null)
    {
        _templateService = templateService;
        _launchDirectory = Path.GetFullPath(launchDirectory ?? Environment.CurrentDirectory);
    }

    public InstallationConfig CreateConfiguration(
        FeatureSelection features,
        Dictionary<string, string> credentials,
        Dictionary<string, int> ports)
    {
        return new InstallationConfig
        {
            Features = features,
            Credentials = credentials,
            Ports = ports,
        };
    }

    // Assembles the final compose file from per-selection fragments. The
    // emitted file stays credential-free: every secret is a ${VAR} reference
    // resolved from the generated .env at compose time (locked decisions D2/D12).
    public string GenerateDockerCompose(InstallationConfig config)
    {
        var capabilities = ServiceCatalog.Resolve(config.Features);
        var tokens = BuildTokenDictionary(config);

        // Conditional network attachments; empty token expands to nothing
        tokens["API_OBSERVABILITY_NET"] = capabilities.MonitoringEnabled
            ? "      - observability-net # exposes /metrics to prometheus"
            : string.Empty;
        tokens["NGINX_OBSERVABILITY_NET"] = capabilities.MonitoringEnabled
            ? "      - observability-net"
            : string.Empty;

        tokens["RABBITMQ_ESSENTIA_NET"] = capabilities.Networks.Contains("essentia-net")
            ? "      - essentia-net"
            : string.Empty;

        var services = capabilities.ComposeTemplates
            .Select(path => _templateService.ReplaceTokens(QuoteSourcePaths(_templateService.LoadTemplate(path), tokens["SOURCE_ROOT"]), tokens))
            .ToList();

        tokens["SERVICES"] = string.Join("\n", services);
        tokens["VOLUMES"] = GenerateVolumes(capabilities, config.VolumeNames);
        tokens["NETWORKS"] = GenerateNetworks(capabilities);

        var baseTemplate = _templateService.LoadTemplate("docker-compose/base.yml.template");

        var compose = _templateService.ReplaceTokens(baseTemplate, tokens);

        if (!config.PreserveFixedContainerNames)
        {
            compose = Regex.Replace(compose, @"(?m)^[ \t]*container_name:[^\r\n]*\r?\n", string.Empty);
        }

        if (!string.IsNullOrEmpty(config.ProjectName))
        {
            compose = "name: " + QuoteYaml(config.ProjectName) + "\n" + compose;
        }

        if (compose.Contains("{{", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated Compose contains unresolved installer tokens. No artifacts were written.");

        return compose;
    }

    public string GenerateGarageToml(InstallationConfig config)
    {
        var template = _templateService.LoadTemplate("config/garage.toml.template");

        var tokens = new Dictionary<string, string>
        {
            ["RPC_SECRET"] = EscapeToml(Required(config, "GARAGE_RPC_SECRET")),
            ["ADMIN_TOKEN"] = EscapeToml(Required(config, "GARAGE_ADMIN_TOKEN")),
            ["METRICS_TOKEN"] = EscapeToml(Required(config, "GARAGE_S3_METRICS_TOKEN")),
            ["GENERATED_AT"] = (config.InstalledAt == default ? DateTimeOffset.UtcNow : config.InstalledAt)
                .ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'"),
        };

        return _templateService.ReplaceTokens(template, tokens);
    }

    // Credential-free scrape config; the Garage token is delivered through
    // its own mounted secrets file (locked decision D12)
    public string GeneratePrometheusYml(InstallationConfig? config = null)
    {
        var project = config?.ProjectName ?? "alexandria-preview";

        if (!InstallationConfigurationResolver.IsValidProjectName(project))
            throw new InvalidOperationException("Monitoring requires a valid installation project name.");

        return _templateService.ReplaceTokens(_templateService.LoadTemplate("config/prometheus.yml.template"),
            new Dictionary<string, string> { ["COMPOSE_PROJECT_NAME"] = project });
    }

    public void BeginGeneration(InstallationConfig config, string outputPath)
    {
        ValidateOutputPath(config, outputPath);

        if (config.SchemaVersion != 2 || string.IsNullOrEmpty(config.ProjectName) || config.EnvironmentValues.Count == 0)
            throw new InvalidOperationException("Resolve the effective installation configuration before generating artifacts.");

        var artifacts = BuildArtifacts(config);
        new CertificateService().Validate(outputPath, config.PublicEndpoint.Host, config.Certificates);
        EnvGenerator.Render(config.EnvironmentValues);
        ValidateArtifactPaths(outputPath, artifacts.Keys);

        Directory.CreateDirectory(outputPath);
        if (string.IsNullOrEmpty(config.GenerationId)) config.GenerationId = Guid.NewGuid().ToString("N");
        config.EnvironmentValues["ALEXANDRIA_GENERATION_ID"] = config.GenerationId;

        var checkpoint = new GenerationCheckpoint
        {
            GenerationId = config.GenerationId,
            Configuration = config,
        };

        ProtectedFile.WriteAtomic(
            Path.Combine(outputPath, InstallationConfigurationResolver.GenerationMarkerName),
            JsonSerializer.Serialize(checkpoint, AlexandriaJsonContext.Default.GenerationCheckpoint));
    }

    public void WriteAllConfigFiles(InstallationConfig config, string outputPath)
    {
        ValidateOutputPath(config, outputPath);
        var persistedEnvironment = EnvGenerator.Parse(File.ReadAllText(Path.Combine(outputPath, ".env")));

        if (persistedEnvironment.Count != config.EnvironmentValues.Count ||
            config.EnvironmentValues.Any(pair => !persistedEnvironment.TryGetValue(pair.Key, out var value) || pair.Value != value))
            throw new InvalidOperationException("The persisted .env does not match the resolved configuration. Metadata and consumer files were preserved.");

        var artifacts = BuildArtifacts(config);
        ValidateArtifactPaths(outputPath, artifacts.Keys);

        var markerPath = Path.Combine(outputPath, InstallationConfigurationResolver.GenerationMarkerName);
        var checkpoint = ReadCheckpoint(markerPath);

        if (checkpoint is null || checkpoint.GenerationId != config.GenerationId || checkpoint.Stage != "writing")
            throw new InvalidOperationException("Begin artifact generation before replacing the environment or consumer files.");

        checkpoint.ArtifactHashes[".env"] = HashFile(Path.Combine(outputPath, ".env"));
        var certificate = new CertificateService().EnsureCertificates(outputPath, config.PublicEndpoint.Host, config.Certificates);
        artifacts["nginx/CLIENT-TRUST.txt"] = CertificateTrustInstructions.Render(config, certificate);

        foreach (var certificatePath in new[] { certificate.CertificatePath, certificate.PrivateKeyPath, certificate.TrustCertificatePath }.Distinct())
        {
            checkpoint.ArtifactHashes[Path.GetRelativePath(outputPath, certificatePath)] = HashFile(certificatePath);
        }

        if (certificate.IsGenerated)
        {
            checkpoint.ArtifactHashes["nginx/certs/local-ca.key"] = HashFile(Path.Combine(outputPath, "nginx/certs/local-ca.key"));
        }
        else if (File.Exists(Path.Combine(outputPath, "nginx/certs/import-source.sha256")))
        {
            checkpoint.ArtifactHashes["nginx/certs/import-source.sha256"] = HashFile(Path.Combine(outputPath, "nginx/certs/import-source.sha256"));
        }

        foreach (var (relativePath, content) in artifacts)
        {
            var path = Path.Combine(outputPath, relativePath);
            var error = PathResolver.ValidateTarget(Path.GetDirectoryName(path)!, probeWritable: false);
            if (error is not null) throw new InvalidOperationException(error);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var mode = relativePath is "rabbitmq/rabbitmq.conf" or "nginx/nginx.conf" or "nginx/CLIENT-TRUST.txt"
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead
                : UnixFileMode.UserRead | UnixFileMode.UserWrite;
            ProtectedFile.WriteAtomic(path, content, mode);
            checkpoint.ArtifactHashes[relativePath] = HashFile(path);
        }

        Directory.CreateDirectory(Path.Combine(outputPath, "init-output"));

        if (config.Stage is "draft" or "configuration" or "generating") config.Stage = "artifacts-ready";
        SaveConfiguration(config, outputPath);
        checkpoint.Configuration = config;
        checkpoint.Stage = "complete";
        ProtectedFile.WriteAtomic(markerPath, JsonSerializer.Serialize(checkpoint, AlexandriaJsonContext.Default.GenerationCheckpoint));
    }

    public void SaveConfiguration(InstallationConfig config, string path)
    {
        ValidateOutputPath(config, path);

        if (config.SchemaVersion != 2 || string.IsNullOrEmpty(config.ProjectName))
            throw new InvalidOperationException("Only resolved versioned installation metadata can be saved. Legacy credentials must first be persisted and verified in .env.");

        var environmentPath = Path.Combine(path, ".env");

        if (!File.Exists(environmentPath) || config.EnvironmentValues.Count == 0)
            throw new InvalidOperationException("Persist and verify the effective .env before saving non-secret installation metadata.");

        var persisted = EnvGenerator.Parse(File.ReadAllText(environmentPath));

        if (persisted.Count != config.EnvironmentValues.Count ||
            config.EnvironmentValues.Any(pair => !persisted.TryGetValue(pair.Key, out var value) || pair.Value != value))
            throw new InvalidOperationException("The effective credentials were not verified in .env. Existing metadata was preserved.");

        var markerPath = Path.Combine(path, InstallationConfigurationResolver.GenerationMarkerName);
        var checkpoint = ReadCheckpoint(markerPath);

        if (checkpoint is not null && (checkpoint.GenerationId != config.GenerationId ||
            checkpoint.Configuration.ProjectName != config.ProjectName))
            throw new InvalidOperationException("The generation checkpoint identifies different installation settings. Existing metadata was preserved.");

        var json = JsonSerializer.Serialize(
            config,
            AlexandriaJsonContext.Default.InstallationConfig
        );

        var metadataPath = Path.Combine(path, "alexandria-config.json");

        if (File.Exists(metadataPath) && InstallationConfigurationResolver.ReadMetadataFile(metadataPath).SchemaVersion == 1)
        {
            var recovery = Path.Combine(path, "recovery");
            var recoveryError = PathResolver.ValidateTarget(recovery, probeWritable: false);
            if (recoveryError is not null) throw new InvalidOperationException(recoveryError);
            Directory.CreateDirectory(recovery);
            var legacy = File.ReadAllText(metadataPath);
            var backupPath = Path.Combine(recovery, "alexandria-config.legacy.json");

            if (File.Exists(backupPath) && File.ReadAllText(backupPath) != legacy)
                throw new InvalidOperationException("The existing legacy recovery copy differs from the metadata. Preserve both copies and inspect the migration before continuing.");

            ProtectedFile.WriteAtomic(backupPath, legacy);
        }

        ProtectedFile.WriteAtomic(metadataPath, json);

        if (checkpoint is { Stage: "complete" })
        {
            checkpoint.Configuration = config;
            checkpoint.ArtifactHashes[".env"] = HashFile(environmentPath);
            ProtectedFile.WriteAtomic(markerPath, JsonSerializer.Serialize(checkpoint, AlexandriaJsonContext.Default.GenerationCheckpoint));
        }
    }

    public InstallationConfig? LoadConfiguration(string path)
    {
        var configPath = Path.Combine(path, "alexandria-config.json");
        if (!File.Exists(configPath)) return null;

        var config = InstallationConfigurationResolver.ReadMetadataFile(configPath);

        if (!PathResolver.TryResolve(config.InstallPath, _launchDirectory, out var target, out var error) ||
            !PathResolver.TryResolve(config.SourceRoot, _launchDirectory, out var source, out error))
        {
            throw new InvalidOperationException($"Saved installation paths are invalid: {error}");
        }

        config.InstallPath = target;
        config.SourceRoot = source;

        return config;
    }

    private Dictionary<string, string> BuildTokenDictionary(InstallationConfig config)
    {
        var tokens = new Dictionary<string, string>();

        foreach (var (key, value) in config.Ports)
        {
            tokens[key] = value.ToString();
        }

        // Generated artifacts live target-relative; only repo-static assets
        // need the checkout prefix when installing outside the source tree
        tokens["SOURCE_ROOT"] = IsExternalTarget(config) ? config.SourceRoot : ".";

        return tokens;
    }

    private static bool IsExternalTarget(InstallationConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.InstallPath) || string.IsNullOrWhiteSpace(config.SourceRoot))
        {
            return false;
        }

        return !PathResolver.PathsEqual(config.InstallPath, config.SourceRoot);
    }

    private static string GenerateVolumes(ResolvedCapabilities capabilities, IReadOnlyDictionary<string, string> volumeNames)
    {
        var volumes = capabilities.Volumes.SelectMany(volume => new[] { $"  {volume}:", "    driver: local" });

        var result = string.Join("\n", volumes);

        foreach (var (key, physicalName) in volumeNames)
        {
            if (!Regex.IsMatch(key, "^[a-z0-9_]+$") || !Regex.IsMatch(physicalName, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$"))
                throw new InvalidOperationException("The recorded Docker volume identity is invalid. Inspect the installation's volume mapping.");

            result = result.Replace($"  {key}:\n    driver: local", $"  {key}:\n    driver: local\n    name: {QuoteYaml(physicalName)}", StringComparison.Ordinal);
        }

        return result;
    }

    private Dictionary<string, string> BuildArtifacts(InstallationConfig config)
    {
        var brokerTemplate = File.ReadAllText(Path.Combine(config.SourceRoot, "rabbitmq", "definitions.template.json"));
        var brokerConfiguration = File.ReadAllText(Path.Combine(config.SourceRoot, "rabbitmq", "rabbitmq.template.conf"))
            .Replace("/etc/rabbitmq/definitions.json", "/etc/rabbitmq/definitions/definitions.json", StringComparison.Ordinal);

        if (brokerConfiguration.Contains("{{", StringComparison.Ordinal) || brokerConfiguration.Contains("${", StringComparison.Ordinal))
            throw new InvalidOperationException("The RabbitMQ configuration has unresolved substitutions. No consumer artifacts were written.");

        var artifacts = new Dictionary<string, string>
        {
            ["docker-compose.yml"] = GenerateDockerCompose(config),
            ["garage.toml"] = GenerateGarageToml(config),
            ["rabbitmq/rabbitmq.conf"] = brokerConfiguration,
            ["rabbitmq/definitions.json"] = BrokerDefinitionsRenderer.Render(brokerTemplate, config.EnvironmentValues),
            ["nginx/nginx.conf"] = NginxConfigurationRenderer.Render(config.PublicEndpoint.Origin, config.Features.IsEnabled(FeatureCatalog.Monitoring.Id)),
        };

        if (config.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            artifacts["prometheus/prometheus.yml"] = GeneratePrometheusYml(config);
            artifacts["prometheus/secrets/garage-metrics-token"] = Required(config, "GARAGE_S3_METRICS_TOKEN") + "\n";
        }

        return artifacts;
    }

    private static void ValidateOutputPath(InstallationConfig config, string outputPath)
    {
        if (!Path.IsPathFullyQualified(outputPath) || !PathResolver.PathsEqual(config.InstallPath, outputPath))
            throw new InvalidOperationException("Artifacts must be written to the resolved absolute installation folder.");

        var error = PathResolver.ValidateTarget(outputPath, probeWritable: false);
        if (error is not null) throw new InvalidOperationException(error);
    }

    private static void ValidateArtifactPaths(string outputPath, IEnumerable<string> artifacts)
    {
        foreach (var relativePath in artifacts.Concat([
            ".env", "alexandria-config.json", InstallationConfigurationResolver.GenerationMarkerName,
            "init-output/garage-credentials.env",
            "nginx/CLIENT-TRUST.txt", "nginx/certs/server.crt", "nginx/certs/server.key",
            "nginx/certs/local-ca.crt", "nginx/certs/local-ca.key",
            "nginx/certs/import-source.sha256",
        ]))
        {
            var path = Path.Combine(outputPath, relativePath);
            var error = PathResolver.ValidateTarget(Path.GetDirectoryName(path)!, probeWritable: false);

            if (error is not null) throw new InvalidOperationException(error);

            if (Directory.Exists(path))
                throw new InvalidOperationException($"Generated artifact '{relativePath}' is an existing folder. Inspect it before replacing installation settings.");
        }
    }

    private static GenerationCheckpoint? ReadCheckpoint(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var checkpoint = JsonSerializer.Deserialize(File.ReadAllText(path), AlexandriaJsonContext.Default.GenerationCheckpoint);

            if (checkpoint is null || checkpoint.SchemaVersion != 1 || checkpoint.Configuration is null ||
                checkpoint.ArtifactHashes is null || checkpoint.Stage is not ("writing" or "complete"))
                throw new InvalidOperationException("The installation generation checkpoint is unrecognized. Inspect it before replacing settings.");

            return checkpoint;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The installation generation checkpoint is invalid. Restore it before replacing settings.");
        }
    }

    private static string Required(InstallationConfig config, string key)
    {
        if (!config.EnvironmentValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || value.Equals("placeholder", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The effective configuration requires '{key}'. No consumer artifacts were written.");

        return value;
    }

    private static string EscapeToml(string value)
    {
        if (value.Any(char.IsControl))
            throw new InvalidOperationException("Garage credentials contain an unsupported control character.");

        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string QuoteYaml(string value) =>
        "'" + value.Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string QuoteSourcePaths(string template, string sourceRoot)
    {
        var source = sourceRoot.Replace(Path.DirectorySeparatorChar, '/');

        var expanded = Regex.Replace(template, @"(?m)^(?<indent>[ ]*)-[ ]+(?<source>\{\{SOURCE_ROOT\}\}[^:\r\n]*):(?<target>/[^:\r\n]*)(?::(?<mode>ro))?\r?$", match =>
        {
            var indent = match.Groups["indent"].Value;
            var mountSource = match.Groups["source"].Value.Replace("{{SOURCE_ROOT}}", source, StringComparison.Ordinal);
            var mount = indent + "- type: bind\n" + indent + "  source: " + QuoteYaml(mountSource) + "\n" +
                indent + "  target: " + QuoteYaml(match.Groups["target"].Value);

            if (match.Groups["mode"].Success) mount += "\n" + indent + "  read_only: true";

            return mount;
        });

        return Regex.Replace(expanded, @"(?m)^(?<prefix>[ \t]*context:[ \t]*)(?<value>[^\r\n]*\{\{SOURCE_ROOT\}\}[^\r\n]*)", match =>
            match.Groups["prefix"].Value + QuoteYaml(match.Groups["value"].Value.Replace("{{SOURCE_ROOT}}", source, StringComparison.Ordinal)));
    }

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static string GenerateNetworks(ResolvedCapabilities capabilities) =>
        string.Join("\n", capabilities.Networks.SelectMany(network => new[] { $"  {network}:", "    driver: bridge" }));
}
