using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Services;

public class InstallationConfigurationResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-effective-config", Guid.NewGuid().ToString("N"));
    private readonly ICredentialService _credentials = Substitute.For<ICredentialService>();

    public InstallationConfigurationResolverTests()
    {
        Directory.CreateDirectory(_root);
        _credentials.GenerateAllCredentials(Arg.Any<FeatureSelection>()).Returns(_ => CreateCredentials());
        _credentials.GeneratePassword().Returns("new-grafana-password");
    }

    [Fact]
    public void Resolve_freshReviewRepeated_retainsIdentityAndSecretsAndUpdatesDerivedFeatureChoice()
    {
        var context = CreateContext();
        var first = InstallationConfigurationResolver.Resolve(context, _credentials);
        var firstPassword = first.EnvironmentValues["DB_PASSWORD"];
        context.Features.SetEnabled(FeatureCatalog.Lyrics.Id, true);

        var second = InstallationConfigurationResolver.Resolve(context, _credentials);

        second.ProjectName.Should().Be(first.ProjectName);
        second.GenerationId.Should().Be(first.GenerationId);
        second.EnvironmentValues["DB_PASSWORD"].Should().Be(firstPassword);
        second.EnvironmentValues["LYRICS_ENABLED"].Should().Be("true");
        _credentials.Received(1).GenerateAllCredentials(Arg.Any<FeatureSelection>());
        Directory.Exists(context.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Resolve_unrecognizedEnvironment_stopsBeforeGeneratingOrChangingFiles()
    {
        var context = CreateContext();
        Directory.CreateDirectory(context.InstallPath);
        var environment = Path.Combine(context.InstallPath, ".env");
        File.WriteAllText(environment, "DB_PASSWORD=only-existing-secret\nUNKNOWN=preserve\n");

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        action.Should().Throw<InvalidOperationException>().WithMessage("*without recognized Alexandria metadata*");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
        File.ReadAllText(environment).Should().Contain("only-existing-secret");
        Directory.GetFiles(context.InstallPath).Should().ContainSingle();
    }

    [Fact]
    public void Resolve_recognizedRepair_preservesActualCredentialsUnknownKeysAndProject()
    {
        var context = CreateRecognizedInstallation();
        var original = EnvGenerator.Parse(File.ReadAllText(Path.Combine(context.InstallPath, ".env")));
        context.Features.SetEnabled(FeatureCatalog.MediaProcessing.Id, true);

        var effective = InstallationConfigurationResolver.Resolve(context, _credentials);

        effective.ProjectName.Should().Be("alexandria-established");
        effective.EnvironmentValues["DB_PASSWORD"].Should().Be(original["DB_PASSWORD"]);
        effective.EnvironmentValues["CUSTOM_SETTING"].Should().Be("keep $literal # exactly");
        effective.Features.IsEnabled(FeatureCatalog.MediaProcessing.Id).Should().BeTrue();
        effective.Credentials["DB_PASSWORD"].Should().Be(original["DB_PASSWORD"]);
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Resolve_reviewedEndpointChange_updatesEveryOriginWithoutRotatingCredentials()
    {
        var context = CreateRecognizedInstallation();
        var original = EnvGenerator.Parse(File.ReadAllText(Path.Combine(context.InstallPath, ".env")));
        context.Config.PublicEndpoint = new PublicEndpoint { Host = "files.example", HttpsPort = 9443 };

        var effective = InstallationConfigurationResolver.Resolve(context, _credentials);

        foreach (var key in new[] { "BASE_URL", "GARAGE_S3_PUBLIC_ENDPOINT", "CORS_ORIGIN", "CORS_ALLOWED_ORIGINS__0" })
        {
            effective.EnvironmentValues[key].Should().Be("https://files.example:9443");
        }

        effective.EnvironmentValues["GRAFANA_ROOT_URL"].Should().Be("https://files.example:9443/monitoring/");
        effective.EnvironmentValues.Should().NotContainKey("CORS_ALLOWED_ORIGINS__1");
        effective.EnvironmentValues["DB_PASSWORD"].Should().Be(original["DB_PASSWORD"]);
        effective.EnvironmentValues["CUSTOM_SETTING"].Should().Be(original["CUSTOM_SETTING"]);
        effective.Ports.Should().ContainSingle().Which.Key.Should().Be("HTTPS_PORT");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Theory]
    [InlineData("DB_PASSWORD")]
    [InlineData("RABBITMQ_PASSWORD")]
    [InlineData("PLAYLIST_WORKER_RABBITMQ_PASSWORD")]
    public void Resolve_establishedSecretMissing_stopsWithoutGuessing(string missingKey)
    {
        var context = CreateRecognizedInstallation();
        var environment = Path.Combine(context.InstallPath, ".env");
        var values = EnvGenerator.Parse(File.ReadAllText(environment));
        values.Remove(missingKey);
        ProtectedFile.WriteAtomic(environment, EnvGenerator.Render(values));
        var original = File.ReadAllText(environment);

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        action.Should().Throw<InvalidOperationException>().WithMessage($"*'{missingKey}' is missing*");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
        File.ReadAllText(environment).Should().Be(original);
    }

    [Fact]
    public void Resolve_durableSecretMissingWithPreviouslyResolvedDraft_stopsInsteadOfRestoringAnUnverifiedMemoryValue()
    {
        var context = CreateRecognizedInstallation();
        InstallationConfigurationResolver.Resolve(context, _credentials);
        context.Config.EnvironmentValues.Should().ContainKey("DB_PASSWORD");
        var environment = Path.Combine(context.InstallPath, ".env");
        var values = EnvGenerator.Parse(File.ReadAllText(environment));
        values.Remove("DB_PASSWORD");
        ProtectedFile.WriteAtomic(environment, EnvGenerator.Render(values));

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'DB_PASSWORD' is missing*");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Resolve_freshDraftMonitoringAndAccountChanged_preservesMachineSecretsAndAppliesNewChoices()
    {
        var context = CreateContext();
        _credentials.GenerateAllCredentials(Arg.Any<FeatureSelection>()).Returns(_ =>
        {
            var credentials = CreateCredentials();
            credentials.Remove("GRAFANA_ADMIN_PASSWORD");

            return credentials;
        });
        var initial = InstallationConfigurationResolver.Resolve(context, _credentials);
        context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);
        context.AdminAccount = new AdminAccountInput { Email = "updated@example.test", Username = "updated", Password = "Updated1!" };

        var revised = InstallationConfigurationResolver.Resolve(context, _credentials);
        var reviewedAgain = InstallationConfigurationResolver.Resolve(context, _credentials);

        revised.EnvironmentValues["DB_PASSWORD"].Should().Be(initial.EnvironmentValues["DB_PASSWORD"]);
        revised.EnvironmentValues["ADMIN_EMAIL"].Should().Be("updated@example.test");
        revised.EnvironmentValues["ADMIN_USERNAME"].Should().Be("updated");
        revised.EnvironmentValues["ADMIN_PASSWORD"].Should().Be("Updated1!");
        revised.EnvironmentValues["GRAFANA_PASSWORD"].Should().Be("new-grafana-password");
        reviewedAgain.EnvironmentValues["GRAFANA_PASSWORD"].Should().Be(revised.EnvironmentValues["GRAFANA_PASSWORD"]);
        _credentials.Received(1).GenerateAllCredentials(Arg.Any<FeatureSelection>());
        _credentials.Received(1).GeneratePassword();
    }

    [Theory]
    [InlineData("GARAGE_METRICS_TOKEN", "GARAGE_S3_METRICS_TOKEN")]
    [InlineData("GRAFANA_ADMIN_PASSWORD", "GRAFANA_PASSWORD")]
    public void Resolve_conflictingAliases_stopsWithRedactedDiagnostic(string alias, string canonical)
    {
        var context = CreateRecognizedInstallation();
        var environment = Path.Combine(context.InstallPath, ".env");
        var values = EnvGenerator.Parse(File.ReadAllText(environment));
        values[alias] = "conflicting-legacy-private-value";
        values[canonical] = "conflicting-canonical-private-value";
        ProtectedFile.WriteAtomic(environment, EnvGenerator.Render(values));

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        var exception = action.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(alias).And.Contain(canonical).And.Contain("disagree");
        exception.Message.Should().NotContain("private-value");
    }

    [Fact]
    public void Resolve_existingLegacyAlias_survivesTheActualEnvironmentRewriteWithItsCanonicalValue()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        fixture.Generate(config);
        var environment = Path.Combine(fixture.Target, ".env");
        var values = EnvGenerator.Parse(File.ReadAllText(environment));
        var expected = values["GARAGE_S3_METRICS_TOKEN"];
        values.Remove("GARAGE_S3_METRICS_TOKEN");
        values["GARAGE_METRICS_TOKEN"] = expected;
        ProtectedFile.WriteAtomic(environment, EnvGenerator.Render(values));

        var effective = fixture.Resolve();
        fixture.Generate(effective);

        var persisted = EnvGenerator.Parse(File.ReadAllText(environment));
        persisted["GARAGE_METRICS_TOKEN"].Should().Be(expected);
        persisted["GARAGE_S3_METRICS_TOKEN"].Should().Be(expected);
        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeFalse();
    }

    [Fact]
    public void Resolve_monitoringAddedOnRepair_reusesReviewedNewSecretBeforeItIsPersisted()
    {
        var context = CreateRecognizedInstallation();
        var environment = Path.Combine(context.InstallPath, ".env");
        var values = EnvGenerator.Parse(File.ReadAllText(environment));
        values.Remove("GRAFANA_PASSWORD");
        ProtectedFile.WriteAtomic(environment, EnvGenerator.Render(values));
        context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);

        var first = InstallationConfigurationResolver.Resolve(context, _credentials);
        var second = InstallationConfigurationResolver.Resolve(context, _credentials);

        second.EnvironmentValues["GRAFANA_PASSWORD"].Should().Be(first.EnvironmentValues["GRAFANA_PASSWORD"]);
        _credentials.Received(1).GeneratePassword();
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Resolve_authoritativeGarageKeys_replacePendingValuesWithoutChangingEstablishedSecrets()
    {
        var context = CreateContext();
        var first = InstallationConfigurationResolver.Resolve(context, _credentials);
        context.GarageCredentials = new GarageCredentialSet("GK-app", "app-secret", "GK-worker", "worker-secret", "GK-stream", "stream-secret");

        var effective = InstallationConfigurationResolver.Resolve(context, _credentials);

        effective.EnvironmentValues["GARAGE_S3_ACCESS_KEY"].Should().Be("GK-app");
        effective.EnvironmentValues["GARAGE_S3_WORKER_SECRET_KEY"].Should().Be("worker-secret");
        effective.EnvironmentValues["DB_PASSWORD"].Should().Be(first.EnvironmentValues["DB_PASSWORD"]);
        effective.Credentials["GARAGE_S3_ACCESS_KEY"].Should().Be("GK-app");
        _credentials.Received(1).GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Resolve_legacyOnlySecretCopy_requiresIdentityAndMigratesWithoutLosingSecret()
    {
        var context = CreateContext();
        Directory.CreateDirectory(context.InstallPath);
        var legacy = CreateCredentials();
        var metadataPath = Path.Combine(context.InstallPath, "alexandria-config.json");
        var original = RenderLegacy(context.Config, legacy);
        File.WriteAllText(metadataPath, original);

        var inspection = InstallationConfigurationResolver.Inspect(context);
        inspection.IsLegacy.Should().BeTrue();
        inspection.SavedConfiguration!.Credentials["DB_PASSWORD"].Should().Be(legacy["DB_PASSWORD"]);

        var unconfirmed = () => InstallationConfigurationResolver.Resolve(context, _credentials);
        unconfirmed.Should().Throw<InvalidOperationException>().WithMessage("*Confirm its existing Docker project*");
        File.ReadAllText(metadataPath).Should().Be(original);

        var identity = new LegacyInstallationIdentity("legacy-project", new Dictionary<string, string>
        {
            ["postgres_local_data"] = "old_project_postgres_local_data",
            ["rabbitmq_local_data"] = "old_project_rabbitmq_local_data",
            ["garage_local_meta_data"] = "old_project_garage_local_meta_data",
            ["garage_local_storage_data"] = "old_project_garage_local_storage_data",
        });
        var effective = InstallationConfigurationResolver.Resolve(context, _credentials, identity);

        effective.ProjectName.Should().Be("legacy-project");
        effective.PreserveFixedContainerNames.Should().BeTrue();
        effective.VolumeNames["postgres_local_data"].Should().Be("old_project_postgres_local_data");
        effective.EnvironmentValues["DB_PASSWORD"].Should().Be(legacy["DB_PASSWORD"]);
        effective.EnvironmentValues["DB_NAME"].Should().Be("alexandria_dev");
        effective.EnvironmentValues["GARAGE_S3_METRICS_TOKEN"].Should().Be(legacy["GARAGE_METRICS_TOKEN"]);
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
        File.ReadAllText(metadataPath).Should().Be(original);
    }

    [Fact]
    public void Resolve_legacyIdentityMissingCoreVolume_stopsBeforeGeneratingOrReplacingCredentials()
    {
        var context = CreateContext();
        Directory.CreateDirectory(context.InstallPath);
        var metadataPath = Path.Combine(context.InstallPath, "alexandria-config.json");
        var original = RenderLegacy(context.Config, CreateCredentials());
        File.WriteAllText(metadataPath, original);
        var identity = new LegacyInstallationIdentity("legacy-project", new Dictionary<string, string>());

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials, identity);

        action.Should().Throw<InvalidOperationException>().WithMessage("*physical 'postgres_local_data' volume*");
        File.ReadAllText(metadataPath).Should().Be(original);
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Resolve_garageTomlConflictsWithEnvironment_stopsWithoutPrintingEitherSecret()
    {
        var context = CreateRecognizedInstallation();
        File.WriteAllText(Path.Combine(context.InstallPath, "garage.toml"),
            "rpc_secret = \"different-private-value\"\nadmin_token = \"admin\"\nmetrics_token = \"metrics\"\n");

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        var exception = action.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("conflicts").And.Contain("GARAGE_RPC_SECRET");
        exception.Message.Should().NotContain("different-private-value");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    [Fact]
    public void Inspect_unknownMetadataSchema_stopsAndPreservesFile()
    {
        var context = CreateContext();
        Directory.CreateDirectory(context.InstallPath);
        var path = Path.Combine(context.InstallPath, "alexandria-config.json");
        const string metadata = "{\"SchemaVersion\":99,\"Features\":{\"Enabled\":[]},\"Ports\":{}}";
        File.WriteAllText(path, metadata);

        var action = () => InstallationConfigurationResolver.Inspect(context);

        action.Should().Throw<InvalidOperationException>().WithMessage("*schema is unsupported*");
        File.ReadAllText(path).Should().Be(metadata);
    }

    [Fact]
    public void Resolve_storageReadyWithPendingKeys_stopsBeforeGeneration()
    {
        var context = CreateRecognizedInstallation();
        var metadataPath = Path.Combine(context.InstallPath, "alexandria-config.json");
        var metadata = JsonSerializer.Deserialize(File.ReadAllText(metadataPath), AlexandriaJsonContext.Default.InstallationConfig)!;
        metadata.Stage = "storage-ready";
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, AlexandriaJsonContext.Default.InstallationConfig));

        var action = () => InstallationConfigurationResolver.Resolve(context, _credentials);

        action.Should().Throw<InvalidOperationException>().WithMessage("*GARAGE_S3_ACCESS_KEY*missing*");
        _credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
    }

    private InstallationContext CreateContext() => new()
    {
        Config = new InstallationConfig
        {
            Features = new FeatureSelection(),
            InstallPath = Path.Combine(_root, "target"),
            SourceRoot = Path.Combine(_root, "source"),
            PublicEndpoint = new PublicEndpoint { Host = "192.0.2.10", HttpsPort = 8443 },
            Ports = new Dictionary<string, int> { ["HTTP_PORT"] = 8080, ["HTTPS_PORT"] = 8443, ["GRAFANA_PORT"] = 3000 },
        },
        AdminAccount = new AdminAccountInput { Email = "synthetic@example.test", Username = "synthetic", Password = "Synthetic1!" },
    };

    private InstallationContext CreateRecognizedInstallation()
    {
        var context = CreateContext();
        var config = InstallationConfigurationResolver.Resolve(context, _credentials);
        config.ProjectName = "alexandria-established";
        config.Stage = "artifacts-ready";
        config.EnvironmentValues["CUSTOM_SETTING"] = "keep $literal # exactly";
        Directory.CreateDirectory(context.InstallPath);
        EnvGenerator.WriteFile(Path.Combine(context.InstallPath, ".env"), config.EnvironmentValues);
        new ConfigurationService(new TemplateService("Local")).SaveConfiguration(config, context.InstallPath);
        context.Config = new InstallationConfig
        {
            InstallPath = context.InstallPath,
            SourceRoot = context.SourceRoot,
            Features = new FeatureSelection(),
            Ports = config.Ports,
        };
        _credentials.ClearReceivedCalls();

        return context;
    }

    internal static Dictionary<string, string> CreateCredentials() => new()
    {
        ["DB_PASSWORD"] = "db;\"password\\$#=✓",
        ["GARAGE_RPC_SECRET"] = "rpc-secret",
        ["GARAGE_ADMIN_TOKEN"] = "garage-admin-secret",
        ["GARAGE_METRICS_TOKEN"] = "garage-metrics-secret",
        ["RABBITMQ_USER"] = "alexandria",
        ["RABBITMQ_PASSWORD"] = "broker-password",
        ["RABBITMQ_VHOST"] = "alexandria",
        ["API_RABBITMQ_USER"] = "api_user",
        ["API_RABBITMQ_PASSWORD"] = "api-password",
        ["RABBITMQ_DOCUMENT_WORKER"] = "document_worker_user",
        ["DOCUMENT_WORKER_RABBITMQ_PASSWORD"] = "document-password",
        ["RABBITMQ_MEDIA_WORKER"] = "media_worker_user",
        ["MEDIA_WORKER_RABBITMQ_PASSWORD"] = "media-password",
        ["RABBITMQ_TRANSPILATION_WORKER"] = "media_transpilation_worker",
        ["TRANSPILATION_WORKER_RABBITMQ_PASSWORD"] = "transpilation-password",
        ["RABBITMQ_MEDIA_METADATA_WORKER"] = "media_metadata_worker_user",
        ["MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD"] = "metadata-password",
        ["RABBITMQ_LYRICS_WORKER"] = "lyrics_worker_user",
        ["LYRICS_WORKER_RABBITMQ_PASSWORD"] = "lyrics-password",
        ["RABBITMQ_PLAYLIST_WORKER"] = "playlist_worker_user",
        ["PLAYLIST_WORKER_RABBITMQ_PASSWORD"] = "playlist-password",
        ["RABBITMQ_ESSENTIA_WORKER"] = "essentia_worker_user",
        ["ESSENTIA_WORKER_RABBITMQ_PASSWORD"] = "essentia-password",
        ["JWT_SECRET"] = "jwt-secret",
        ["JWT_ISSUER"] = "alexandria",
        ["JWT_AUDIENCE"] = "alexandria-users",
        ["CSRF_SECRET"] = "csrf-secret",
        ["POSTGRES_EXPORTER_PASSWORD"] = "exporter-password",
        ["GRAFANA_USER"] = "admin",
        ["GRAFANA_ADMIN_PASSWORD"] = "grafana-password",
    };

    private static string RenderLegacy(InstallationConfig config, Dictionary<string, string> credentials)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(config, AlexandriaJsonContext.Default.InstallationConfig));
        using var output = new MemoryStream();

        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name is "SchemaVersion" or "ProjectName" or "Stage" or "GenerationId") continue;
                property.WriteTo(writer);
            }

            writer.WriteStartObject("Credentials");

            foreach (var (key, value) in credentials)
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
