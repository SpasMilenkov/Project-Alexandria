// Services/ConfigurationService.cs
using Builder.Models;
using Environment = Builder.Models.Environment;

namespace Builder.Services;

public interface IConfigurationService
{
    InstallationConfig CreateConfiguration(Environment env, S3Provider provider);
    string GenerateDockerCompose(InstallationConfig config);
    void SaveConfiguration(InstallationConfig config, string path);
    InstallationConfig? LoadConfiguration(string path);
}

public class ConfigurationService : IConfigurationService
{
    private readonly ITemplateService _templateService;
    
    private readonly Dictionary<string, int> _defaultPorts = new()
    {
        { "postgres", 5432 },
        { "s3_api", 9000 },
        { "s3_console", 9001 },
        { "rabbitmq", 5672 },
        { "rabbitmq_management", 15672 },
        { "garage_rpc", 3901 },
        { "garage_web", 3902 }
    };

    public ConfigurationService(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    public InstallationConfig CreateConfiguration(Environment env, S3Provider provider)
    {
        var config = new InstallationConfig
        {
            Environment = env,
            S3Provider = provider,
            Ports = new Dictionary<string, int>(_defaultPorts),
            Credentials = GenerateCredentials()
        };
        
        return config;
    }

    public string GenerateDockerCompose(InstallationConfig config)
    {
        // Load templates
        var baseTemplate = _templateService.LoadTemplate("docker-compose/base.yml");
        var postgresTemplate = _templateService.LoadTemplate("docker-compose/postgres.yml");
        var rabbitmqTemplate = _templateService.LoadTemplate("docker-compose/rabbitmq.yml");
        
        // Load S3 provider template
        var s3Template = config.S3Provider switch
        {
            S3Provider.Garage => _templateService.LoadTemplate("docker-compose/garage.yml"),
            S3Provider.MinIO => _templateService.LoadTemplate("docker-compose/minio.yml"),
            S3Provider.RustFS => _templateService.LoadTemplate("docker-compose/rustfs.yml"),
            _ => throw new ArgumentException("Invalid S3 provider")
        };

        // Build tokens dictionary
        var tokens = new Dictionary<string, string>
        {
            { "ENVIRONMENT", config.Environment.ToString().ToLower() },
            { "POSTGRES_PORT", config.Ports["postgres"].ToString() },
            { "S3_API_PORT", config.Ports["s3_api"].ToString() },
            { "S3_CONSOLE_PORT", config.Ports["s3_console"].ToString() },
            { "RABBITMQ_PORT", config.Ports["rabbitmq"].ToString() },
            { "RABBITMQ_MANAGEMENT_PORT", config.Ports["rabbitmq_management"].ToString() },
            { "DB_PASSWORD", config.Credentials["db_password"] },
            { "S3_ACCESS_KEY", config.Credentials["s3_access_key"] },
            { "S3_SECRET_KEY", config.Credentials["s3_secret_key"] },
            { "RABBITMQ_USER", config.Credentials["rabbitmq_user"] },
            { "RABBITMQ_PASSWORD", config.Credentials["rabbitmq_password"] }
        };

        // Add Garage-specific ports if needed
        if (config.S3Provider == S3Provider.Garage)
        {
            tokens.Add("GARAGE_RPC_PORT", config.Ports["garage_rpc"].ToString());
            tokens.Add("GARAGE_WEB_PORT", config.Ports["garage_web"].ToString());
        }

        // Replace tokens in each service template
        var postgresService = _templateService.ReplaceTokens(postgresTemplate, tokens);
        var s3Service = _templateService.ReplaceTokens(s3Template, tokens);
        var rabbitmqService = _templateService.ReplaceTokens(rabbitmqTemplate, tokens);

        // Combine services
        var allServices = string.Join("\n", postgresService, s3Service, rabbitmqService);

        // Generate volumes section
        var volumes = GenerateVolumes(config);

        // Replace in base template
        tokens.Clear();
        tokens.Add("SERVICES", allServices);
        tokens.Add("VOLUMES", volumes);

        var finalCompose = _templateService.ReplaceTokens(baseTemplate, tokens);
        
        return finalCompose;
    }

    private string GenerateVolumes(InstallationConfig config)
    {
        var volumes = new List<string>
        {
            "  postgres_data:",
            "    driver: local"
        };

        switch (config.S3Provider)
        {
            case S3Provider.Garage:
                volumes.AddRange(new[]
                {
                    "  garage_meta:",
                    "    driver: local",
                    "  garage_data:",
                    "    driver: local"
                });
                break;
            case S3Provider.MinIO:
                volumes.AddRange(new[]
                {
                    "  minio_data:",
                    "    driver: local"
                });
                break;
            case S3Provider.RustFS:
                volumes.AddRange(new[]
                {
                    "  rustfs_data:",
                    "    driver: local"
                });
                break;
        }

        volumes.AddRange(new[]
        {
            "  rabbitmq_data:",
            "    driver: local"
        });

        return string.Join("\n", volumes);
    }

    private Dictionary<string, string> GenerateCredentials()
    {
        return new Dictionary<string, string>
        {
            { "db_password", GenerateSecurePassword() },
            { "s3_access_key", GenerateAccessKey() },
            { "s3_secret_key", GenerateSecurePassword() },
            { "rabbitmq_user", "admin" },
            { "rabbitmq_password", GenerateSecurePassword() }
        };
    }

    private string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 24)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private string GenerateAccessKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 20)
            .Select(s => s[random.Next(s.Length)]).ToArray());
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
        var json = File.ReadAllText(Path.Combine(path, "alexandria-config.json"));
        return System.Text.Json.JsonSerializer.Deserialize(
            json, 
            AlexandriaJsonContext.Default.InstallationConfig
        );
    }
}