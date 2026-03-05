using Builder.Models;
using Environment = Builder.Models.Environment;

namespace Builder.Services;

public interface IConfigurationService
{
    InstallationConfig CreateConfiguration(Environment env, FeatureSelection features, Dictionary<string, string> credentials, Dictionary<string, int> ports);
    string GenerateDockerCompose(InstallationConfig config);
    string GenerateComposeOverride(InstallationConfig config);
    string GenerateEnvFile(InstallationConfig config);
    string GenerateGarageConfig(InstallationConfig config);
    string GeneratePrometheusConfig();
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
        Environment env,
        FeatureSelection features,
        Dictionary<string, string> credentials,
        Dictionary<string, int> ports)
    {
        return new InstallationConfig
        {
            Environment = env,
            S3Provider = S3Provider.Garage,
            Features = features,
            Credentials = credentials,
            Ports = ports,
            InstalledAt = DateTime.UtcNow
        };
    }

    public string GenerateDockerCompose(InstallationConfig config)
    {
        var baseTemplate = _templateService.LoadTemplate("docker-compose/base.yml");
        var tokens = BuildTokenDictionary(config);

        // Always-included services
        var serviceTemplates = new List<string>
        {
            "docker-compose/postgres.yml",
            "docker-compose/garage.yml",
            "docker-compose/garage-init.yml",
            "docker-compose/rabbitmq.yml",
            "docker-compose/api.yml",
            "docker-compose/document-worker.yml",
            "docker-compose/frontend.yml",
            "docker-compose/postgres-backup.yml",
        };

        // Optional services
        if (config.Features.MediaProcessing)
            serviceTemplates.Add("docker-compose/media-worker.yml");

        if (config.Features.Monitoring)
        {
            serviceTemplates.Add("docker-compose/prometheus.yml");
            serviceTemplates.Add("docker-compose/grafana.yml");
        }

        // Load and process each service template
        var services = new List<string>();
        foreach (var templatePath in serviceTemplates)
        {
            var template = _templateService.LoadTemplate(templatePath);
            services.Add(_templateService.ReplaceTokens(template, tokens));
        }

        var allServices = string.Join("\n\n", services);
        var volumes = GenerateVolumes(config);

        tokens["SERVICES"] = allServices;
        tokens["VOLUMES"] = volumes;

        return _templateService.ReplaceTokens(baseTemplate, tokens);
    }

    public string GenerateComposeOverride(InstallationConfig config)
    {
        var localPreviewTemplates = new TemplateService("LocalPreview");
        var template = localPreviewTemplates.LoadTemplate("docker-compose.override.yml");
        var tokens = BuildTokenDictionary(config);
        return localPreviewTemplates.ReplaceTokens(template, tokens);
    }

    public string GenerateEnvFile(InstallationConfig config)
    {
        var template = _templateService.LoadTemplate("Config/env.template");
        var tokens = BuildTokenDictionary(config);
        return _templateService.ReplaceTokens(template, tokens);
    }

    public string GenerateGarageConfig(InstallationConfig config)
    {
        var template = _templateService.LoadTemplate("Config/garage.prod.toml.template");
        var tokens = BuildTokenDictionary(config);
        return _templateService.ReplaceTokens(template, tokens);
    }

    public string GeneratePrometheusConfig()
    {
        return _templateService.LoadTemplate("Config/prometheus.yml.template");
    }

    public void WriteAllConfigFiles(InstallationConfig config, string outputPath)
    {
        // docker-compose.yml
        var composeContent = GenerateDockerCompose(config);
        File.WriteAllText(Path.Combine(outputPath, "docker-compose.yml"), composeContent);

        // .env
        var envContent = GenerateEnvFile(config);
        File.WriteAllText(Path.Combine(outputPath, ".env"), envContent);

        // garage.prod.toml
        var garageConfig = GenerateGarageConfig(config);
        File.WriteAllText(Path.Combine(outputPath, "garage.prod.toml"), garageConfig);

        // backups directory
        var backupsDir = Path.Combine(outputPath, "backups");
        if (!Directory.Exists(backupsDir))
            Directory.CreateDirectory(backupsDir);

        // prometheus.yml (if monitoring enabled)
        if (config.Features.Monitoring)
        {
            var prometheusConfig = GeneratePrometheusConfig();
            File.WriteAllText(Path.Combine(outputPath, "prometheus.yml"), prometheusConfig);
        }

        // docker-compose.override.yml (Local Preview only — exposes debug ports)
        if (config.Environment == Environment.LocalPreview)
        {
            var overrideContent = GenerateComposeOverride(config);
            File.WriteAllText(Path.Combine(outputPath, "docker-compose.override.yml"), overrideContent);
        }

        // alexandria-config.json
        SaveConfiguration(config, outputPath);
    }

    public void SaveConfiguration(InstallationConfig config, string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
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
        return System.Text.Json.JsonSerializer.Deserialize(
            json,
            AlexandriaJsonContext.Default.InstallationConfig
        );
    }

    private Dictionary<string, string> BuildTokenDictionary(InstallationConfig config)
    {
        var tokens = new Dictionary<string, string>(config.Credentials);

        // Add port tokens
        foreach (var (key, value) in config.Ports)
        {
            tokens[key] = value.ToString();
        }

        // Add environment
        tokens["ENVIRONMENT"] = config.Environment.ToString().ToLowerInvariant();

        return tokens;
    }

    private string GenerateVolumes(InstallationConfig config)
    {
        var volumes = new List<string>
        {
            "  postgres_data:",
            "    driver: local",
            "  garage_meta_data:",
            "    driver: local",
            "  garage_storage_data:",
            "    driver: local",
            "  rabbitmq_data:",
            "    driver: local",
        };

        if (config.Features.Monitoring)
        {
            volumes.AddRange([
                "  prometheus_data:",
                "    driver: local",
                "  grafana_data:",
                "    driver: local",
            ]);
        }

        return string.Join("\n", volumes);
    }
}
