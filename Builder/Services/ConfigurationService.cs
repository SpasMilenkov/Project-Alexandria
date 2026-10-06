using System.Text.Json;
using Builder.Models;

namespace Builder.Services;

public interface IConfigurationService
{
    InstallationConfig CreateConfiguration(FeatureSelection features, Dictionary<string, string> credentials, Dictionary<string, int> ports);

    string GenerateDockerCompose(InstallationConfig config);

    string GenerateGarageToml(InstallationConfig config);

    string GeneratePrometheusYml();

    void WriteAllConfigFiles(InstallationConfig config, string outputPath);

    void SaveConfiguration(InstallationConfig config, string path);

    InstallationConfig? LoadConfiguration(string path);
}

public class ConfigurationService : IConfigurationService
{
    private readonly ITemplateService _templateService;

    public ConfigurationService(ITemplateService templateService)
    {
        _templateService = templateService;
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
        var features = config.Features;

        var taggingEnabled =
            features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id) ||
            features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id);

        var tokens = BuildTokenDictionary(config);

        // Conditional network attachments; empty token expands to nothing
        tokens["API_OBSERVABILITY_NET"] = features.IsEnabled(FeatureCatalog.Monitoring.Id)
            ? "      - observability-net # exposes /metrics to prometheus"
            : string.Empty;

        tokens["RABBITMQ_ESSENTIA_NET"] = taggingEnabled || features.IsEnabled(FeatureCatalog.Monitoring.Id)
            ? "      - essentia-net"
            : string.Empty;

        var serviceTemplates = new List<string>
        {
            "docker-compose/storage.yml.template",
            "docker-compose/apps.yml.template",
        };

        if (features.IsEnabled(FeatureCatalog.DocumentPreviews.Id))
            serviceTemplates.Add("docker-compose/document-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.MediaProcessing.Id))
            serviceTemplates.Add("docker-compose/media-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.AdaptiveStreaming.Id))
            serviceTemplates.Add("docker-compose/transpilation-worker.yml.template");

        if (taggingEnabled)
            serviceTemplates.Add("docker-compose/media-metadata-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.Lyrics.Id))
            serviceTemplates.Add("docker-compose/lyrics-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id))
            serviceTemplates.Add("docker-compose/essentia-effnet-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id))
            serviceTemplates.Add("docker-compose/essentia-maest-worker.yml.template");

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
            serviceTemplates.Add("docker-compose/monitoring.yml.template");

        var services = serviceTemplates
            .Select(path => _templateService.ReplaceTokens(_templateService.LoadTemplate(path), tokens))
            .ToList();

        tokens["SERVICES"] = string.Join("\n", services);
        tokens["VOLUMES"] = GenerateVolumes(features);
        tokens["NETWORKS"] = GenerateNetworks(features, taggingEnabled);

        var baseTemplate = _templateService.LoadTemplate("docker-compose/base.yml.template");

        return _templateService.ReplaceTokens(baseTemplate, tokens);
    }

    public string GenerateGarageToml(InstallationConfig config)
    {
        var template = _templateService.LoadTemplate("config/garage.toml.template");

        var tokens = new Dictionary<string, string>
        {
            ["RPC_SECRET"] = config.Credentials.GetValueOrDefault("GARAGE_RPC_SECRET", string.Empty),
            ["ADMIN_TOKEN"] = config.Credentials.GetValueOrDefault("GARAGE_ADMIN_TOKEN", string.Empty),
            ["METRICS_TOKEN"] = config.Credentials.GetValueOrDefault("GARAGE_METRICS_TOKEN", string.Empty),
            ["GENERATED_AT"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"),
        };

        return _templateService.ReplaceTokens(template, tokens);
    }

    // Credential-free scrape config; the Garage token is delivered through
    // its own mounted secrets file (locked decision D12)
    public string GeneratePrometheusYml()
    {
        return _templateService.LoadTemplate("config/prometheus.yml.template");
    }

    public void WriteAllConfigFiles(InstallationConfig config, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(Path.Combine(outputPath, "init-output"));

        File.WriteAllText(Path.Combine(outputPath, "docker-compose.yml"), GenerateDockerCompose(config));
        File.WriteAllText(Path.Combine(outputPath, "garage.toml"), GenerateGarageToml(config));

        if (config.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            var prometheusDir = Path.Combine(outputPath, "prometheus");

            Directory.CreateDirectory(prometheusDir);
            Directory.CreateDirectory(Path.Combine(prometheusDir, "secrets"));

            File.WriteAllText(
                Path.Combine(prometheusDir, "prometheus.yml"),
                GeneratePrometheusYml());

            WriteSecretFile(
                Path.Combine(prometheusDir, "secrets", "garage-metrics-token"),
                config.Credentials.GetValueOrDefault("GARAGE_METRICS_TOKEN", string.Empty));
        }

        SaveConfiguration(config, outputPath);
    }

    private static void WriteSecretFile(string path, string content)
    {
        File.WriteAllText(path, content + Environment.NewLine);

        // Owner-only read/write so other local users cannot read the token
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public void SaveConfiguration(InstallationConfig config, string path)
    {
        var json = JsonSerializer.Serialize(
            config,
            AlexandriaJsonContext.Default.InstallationConfig
        );

        File.WriteAllText(Path.Combine(path, "alexandria-config.json"), json);
    }

    public InstallationConfig? LoadConfiguration(string path)
    {
        var configPath = Path.Combine(path, "alexandria-config.json");
        if (!File.Exists(configPath)) return null;

        var json = File.ReadAllText(configPath);

        return JsonSerializer.Deserialize(
            json,
            AlexandriaJsonContext.Default.InstallationConfig
        );
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

        return !string.Equals(
            Path.GetFullPath(config.InstallPath).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(config.SourceRoot).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateVolumes(FeatureSelection features)
    {
        var taggingEnabled =
            features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id) ||
            features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id);

        var volumes = new List<string>
        {
            "  postgres_local_data:",
            "    driver: local",
            "  garage_local_meta_data:",
            "    driver: local",
            "  garage_local_storage_data:",
            "    driver: local",
            "  rabbitmq_local_data:",
            "    driver: local",
            "  rabbitmq_definitions:",
            "    driver: local",
        };

        if (features.IsEnabled(FeatureCatalog.DocumentPreviews.Id))
        {
            volumes.AddRange(["  document_worker_scratch:", "    driver: local"]);
        }

        if (features.IsEnabled(FeatureCatalog.MediaProcessing.Id))
        {
            volumes.AddRange(["  media_worker_scratch:", "    driver: local"]);
        }

        if (features.IsEnabled(FeatureCatalog.AdaptiveStreaming.Id))
        {
            volumes.AddRange(["  transpilation_worker_scratch:", "    driver: local"]);
        }

        if (taggingEnabled)
        {
            volumes.AddRange(["  media_audio_data:", "    driver: local"]);
        }

        if (taggingEnabled || features.IsEnabled(FeatureCatalog.Lyrics.Id))
        {
            volumes.AddRange(["  media_metadata_worker_scratch:", "    driver: local"]);
        }

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            volumes.AddRange([
                "  prometheus_local_data:",
                "    driver: local",
                "  grafana_local_data:",
                "    driver: local",
                "  alloy_local_data:",
                "    driver: local",
                "  loki_local_data:",
                "    driver: local",
            ]);
        }

        return string.Join("\n", volumes);
    }

    private static string GenerateNetworks(FeatureSelection features, bool taggingEnabled)
    {
        var networks = new List<string>
        {
            "  frontend-net:",
            "    driver: bridge",
            "  app-net:",
            "    driver: bridge",
            "  data-net:",
            "    driver: bridge",
        };

        if (taggingEnabled || features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            networks.AddRange(["  essentia-net:", "    driver: bridge"]);
        }

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            networks.AddRange(["  observability-net:", "    driver: bridge"]);
        }

        return string.Join("\n", networks);
    }
}
