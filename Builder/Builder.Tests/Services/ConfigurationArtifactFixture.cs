using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Services;

internal sealed class ConfigurationArtifactFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-artifact-tests", Guid.NewGuid().ToString("N"));

    public InstallationContext Context { get; }
    public ConfigurationService Service { get; } = new(new TemplateService("Local"));
    public ICredentialService Credentials { get; } = Substitute.For<ICredentialService>();
    public string Target => Context.InstallPath;

    public ConfigurationArtifactFixture(bool monitoring = false, string sourceFolder = "source")
    {
        var source = Path.Combine(_root, sourceFolder);
        Directory.CreateDirectory(Path.Combine(source, "rabbitmq"));
        var repository = FindRepositoryRoot();

        foreach (var file in new[] { "definitions.template.json", "rabbitmq.template.conf" })
        {
            File.Copy(Path.Combine(repository, "rabbitmq", file), Path.Combine(source, "rabbitmq", file));
        }

        var features = new FeatureSelection();
        features.SetEnabled(FeatureCatalog.Monitoring.Id, monitoring);
        Context = new InstallationContext
        {
            Config = new InstallationConfig
            {
                Features = features,
                InstallPath = Path.Combine(_root, "target"),
                SourceRoot = source,
                PublicEndpoint = new PublicEndpoint { Host = "192.0.2.10", HttpsPort = 8443 },
                Ports = new Dictionary<string, int> { ["HTTP_PORT"] = 8080, ["HTTPS_PORT"] = 8443, ["GRAFANA_PORT"] = 3000 },
            },
            AdminAccount = new AdminAccountInput { Email = "synthetic@example.test", Username = "synthetic", Password = "Synthetic1!" },
        };
        Credentials.GenerateAllCredentials(Arg.Any<FeatureSelection>())
            .Returns(_ => InstallationConfigurationResolverTests.CreateCredentials());
        Credentials.GeneratePassword().Returns("new-grafana-password");
    }

    public InstallationConfig Resolve() => InstallationConfigurationResolver.Resolve(Context, Credentials);

    public void Generate(InstallationConfig config)
    {
        Service.BeginGeneration(config, Target);
        EnvGenerator.WriteFile(Path.Combine(Target, ".env"), config.EnvironmentValues);
        Service.WriteAllConfigFiles(config, Target);
    }

    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rabbitmq", "definitions.template.json"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("The Builder artifact tests require the repository's broker templates.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
