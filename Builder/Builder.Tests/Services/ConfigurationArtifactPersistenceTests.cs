using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Services;

public class ConfigurationArtifactPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generate_effectiveCredentials_feedEveryPersistedConsumerWithRestrictedSecrets(bool monitoring)
    {
        using var fixture = new ConfigurationArtifactFixture(monitoring);
        var config = fixture.Resolve();
        var values = config.EnvironmentValues;
        values["GARAGE_RPC_SECRET"] = "rpc'\"\\$ #=✓";
        values["GARAGE_ADMIN_TOKEN"] = "admin'\"\\$ #=✓";
        values["GARAGE_S3_METRICS_TOKEN"] = "metrics'\"\\$ #=✓";
        values["RABBITMQ_LYRICS_WORKER"] = "legacy_lyrics_identity";
        values["LYRICS_WORKER_RABBITMQ_PASSWORD"] = "lyrics'\"\\$ #=✓";
        values["RABBITMQ_VHOST"] = "/custom vhost";
        values["DB_CONNECTION_STRING"] = EnvGenerator.BuildDatabaseConnectionString(values);

        fixture.Generate(config);

        var persisted = EnvGenerator.Parse(File.ReadAllText(Path.Combine(fixture.Target, ".env")));
        persisted.Should().BeEquivalentTo(values);
        persisted["ALEXANDRIA_GENERATION_ID"].Should().Be(config.GenerationId);
        var database = new DbConnectionStringBuilder { ConnectionString = persisted["DB_CONNECTION_STRING"] };
        database["Password"].Should().Be(values["DB_PASSWORD"]);

        var toml = File.ReadAllText(Path.Combine(fixture.Target, "garage.toml"));

        foreach (var (tomlKey, envKey) in new[]
        {
            ("rpc_secret", "GARAGE_RPC_SECRET"), ("admin_token", "GARAGE_ADMIN_TOKEN"), ("metrics_token", "GARAGE_S3_METRICS_TOKEN"),
        })
        {
            var literal = Regex.Match(toml, "(?m)^" + tomlKey + " = (?<value>.*)$").Groups["value"].Value;
            using var parsed = JsonDocument.Parse(literal);
            parsed.RootElement.GetString().Should().Be(values[envKey]);
        }

        using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Target, "rabbitmq", "definitions.json")));
        var users = definitions.RootElement.GetProperty("users").EnumerateArray()
            .ToDictionary(user => user.GetProperty("name").GetString()!, user => user.GetProperty("password").GetString()!);

        foreach (var (userKey, passwordKey) in BrokerConsumers)
        {
            users[values[userKey]].Should().Be(values[passwordKey]);
        }

        foreach (var permission in definitions.RootElement.GetProperty("permissions").EnumerateArray())
        {
            users.Should().ContainKey(permission.GetProperty("user").GetString()!);
            permission.GetProperty("vhost").GetString().Should().Be(values["RABBITMQ_VHOST"]);
        }

        File.ReadAllText(Path.Combine(fixture.Target, "rabbitmq", "rabbitmq.conf"))
            .Should().Contain("management.load_definitions = /etc/rabbitmq/definitions/definitions.json");
        var metadata = File.ReadAllText(Path.Combine(fixture.Target, "alexandria-config.json"));
        metadata.Should().NotContain("Credentials").And.NotContain("EnvironmentValues").And.NotContain("Synthetic1!");
        var compose = File.ReadAllText(Path.Combine(fixture.Target, "docker-compose.yml"));
        compose.Should().Contain("name: '" + config.ProjectName + "'").And.NotContain("container_name:");
        compose.Should().NotContain(values["DB_PASSWORD"]).And.NotContain(values["GARAGE_S3_METRICS_TOKEN"]);
        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeFalse();

        foreach (var path in new[] { ".env", "garage.toml", "rabbitmq/definitions.json", "alexandria-config.json", InstallationConfigurationResolver.GenerationMarkerName })
        {
            AssertMode(Path.Combine(fixture.Target, path), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        AssertMode(Path.Combine(fixture.Target, "rabbitmq", "rabbitmq.conf"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        if (monitoring)
        {
            var token = Path.Combine(fixture.Target, "prometheus", "secrets", "garage-metrics-token");
            File.ReadAllText(token).Should().Be(values["GARAGE_S3_METRICS_TOKEN"] + "\n");
            AssertMode(token, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            compose.Should().Contain("user: \"0:0\"");
        }
    }

    [Fact]
    public void SaveConfiguration_storageKeysPersisted_updatesSnapshotAndEnvironmentHashWithoutBlessingModifiedConsumers()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        fixture.Generate(config);
        var markerPath = Path.Combine(fixture.Target, InstallationConfigurationResolver.GenerationMarkerName);
        var before = JsonSerializer.Deserialize(File.ReadAllText(markerPath), AlexandriaJsonContext.Default.GenerationCheckpoint)!;
        foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(
            new GarageCredentialSet("GK-established", "established-storage-secret", "GK-worker", "worker-secret", "GK-stream", "stream-secret")))
        {
            config.EnvironmentValues[key] = value;
        }
        EnvGenerator.WriteFile(Path.Combine(fixture.Target, ".env"), config.EnvironmentValues);
        config.Stage = "storage-ready";

        fixture.Service.SaveConfiguration(config, fixture.Target);

        var after = JsonSerializer.Deserialize(File.ReadAllText(markerPath), AlexandriaJsonContext.Default.GenerationCheckpoint)!;
        after.Configuration.Stage.Should().Be("storage-ready");
        after.ArtifactHashes[".env"].Should().NotBe(before.ArtifactHashes[".env"]);
        after.ArtifactHashes["docker-compose.yml"].Should().Be(before.ArtifactHashes["docker-compose.yml"]);
        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeFalse();
        File.AppendAllText(Path.Combine(fixture.Target, "docker-compose.yml"), "\n# modified consumer\n");

        fixture.Service.SaveConfiguration(config, fixture.Target);

        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeTrue();
        File.ReadAllText(markerPath).Should().NotContain("established-storage-secret");
    }

    [Fact]
    public void BeginGeneration_retryAfterEnvironmentCommit_retainsPendingHandoffIdentityAndEstablishedSecrets()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var original = fixture.Resolve();
        fixture.Service.BeginGeneration(original, fixture.Target);
        EnvGenerator.WriteFile(Path.Combine(fixture.Target, ".env"), original.EnvironmentValues);
        var generation = original.GenerationId;
        var password = original.EnvironmentValues["DB_PASSWORD"];
        var snapshot = InstallationConfigurationResolver.Inspect(fixture.Context);
        snapshot.IsIncompleteGeneration.Should().BeTrue();
        fixture.Context.Config = snapshot.SavedConfiguration!;
        fixture.Credentials.ClearReceivedCalls();

        var recovered = fixture.Resolve();
        fixture.Generate(recovered);

        recovered.GenerationId.Should().Be(generation);
        recovered.EnvironmentValues["ALEXANDRIA_GENERATION_ID"].Should().Be(generation);
        recovered.EnvironmentValues["DB_PASSWORD"].Should().Be(password);
        fixture.Credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeFalse();
    }

    [Fact]
    public void BeginGeneration_interruptedBeforeFirstEnvironmentCommit_retriesFreshWithTheSameProjectIdentity()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var original = fixture.Resolve();
        fixture.Service.BeginGeneration(original, fixture.Target);
        var snapshot = InstallationConfigurationResolver.Inspect(fixture.Context);
        snapshot.IsUncommittedFreshGeneration.Should().BeTrue();
        fixture.Context.Config = snapshot.SavedConfiguration!;
        fixture.Credentials.ClearReceivedCalls();

        var recovered = fixture.Resolve();
        fixture.Generate(recovered);

        recovered.ProjectName.Should().Be(original.ProjectName);
        recovered.GenerationId.Should().Be(original.GenerationId);
        fixture.Credentials.Received(1).GenerateAllCredentials(Arg.Any<FeatureSelection>());
        InstallationConfigurationResolver.Inspect(fixture.Context).IsIncompleteGeneration.Should().BeFalse();
    }

    [Fact]
    public void Inspect_interruptedRepair_prefersReviewedFeatureSnapshotOverOlderMetadata()
    {
        using var fixture = new ConfigurationArtifactFixture();
        fixture.Generate(fixture.Resolve());
        fixture.Context.Features.SetEnabled(FeatureCatalog.Lyrics.Id, true);
        var repair = fixture.Resolve();

        fixture.Service.BeginGeneration(repair, fixture.Target);

        var inspection = InstallationConfigurationResolver.Inspect(fixture.Context);
        inspection.IsIncompleteGeneration.Should().BeTrue();
        inspection.SavedConfiguration!.Features.IsEnabled(FeatureCatalog.Lyrics.Id).Should().BeTrue();
    }

    [Theory]
    [InlineData("PublicEndpoint")]
    [InlineData("Certificates")]
    public void Inspect_nullEndpointOrCertificateCheckpoint_stopsWithoutChangingArtifacts(string setting)
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        fixture.Service.BeginGeneration(config, fixture.Target);
        var markerPath = Path.Combine(fixture.Target, InstallationConfigurationResolver.GenerationMarkerName);
        var marker = JsonNode.Parse(File.ReadAllText(markerPath))!;
        marker["Configuration"]![setting] = null;
        var malformed = marker.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(markerPath, malformed);

        var inspect = () => InstallationConfigurationResolver.Inspect(fixture.Context);

        inspect.Should().Throw<InvalidOperationException>().WithMessage("*marker is unrecognized*");
        File.ReadAllText(markerPath).Should().Be(malformed);
        File.Exists(Path.Combine(fixture.Target, ".env")).Should().BeFalse();
        File.Exists(Path.Combine(fixture.Target, "nginx/certs/server.key")).Should().BeFalse();
    }

    [Fact]
    public void BeginGeneration_invalidConsumerTemplate_preservesExistingEnvironmentAndCheckpoint()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        fixture.Generate(config);
        var environment = File.ReadAllText(Path.Combine(fixture.Target, ".env"));
        var marker = File.ReadAllText(Path.Combine(fixture.Target, InstallationConfigurationResolver.GenerationMarkerName));
        File.WriteAllText(Path.Combine(config.SourceRoot, "rabbitmq", "definitions.template.json"),
            "{\"users\":[{\"name\":\"${unsupported}\"}],\"permissions\":[]}");

        var action = () => fixture.Service.BeginGeneration(config, fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*unsupported substitution*");
        File.ReadAllText(Path.Combine(fixture.Target, ".env")).Should().Be(environment);
        File.ReadAllText(Path.Combine(fixture.Target, InstallationConfigurationResolver.GenerationMarkerName)).Should().Be(marker);
    }

    [Fact]
    public void SaveConfiguration_legacyOnlySecretCopy_requiresVerifiedEnvironmentBeforeRestrictedBackupAndMigration()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        Directory.CreateDirectory(fixture.Target);
        var metadataPath = Path.Combine(fixture.Target, "alexandria-config.json");
        var legacy = JsonSerializer.Serialize(new
        {
            Features = config.Features,
            Ports = config.Ports,
            InstallPath = config.InstallPath,
            SourceRoot = config.SourceRoot,
            Credentials = config.Credentials,
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metadataPath, legacy);

        var prematureSave = () => fixture.Service.SaveConfiguration(config, fixture.Target);

        prematureSave.Should().Throw<InvalidOperationException>().WithMessage("*Persist and verify*");
        File.ReadAllText(metadataPath).Should().Be(legacy);
        Directory.Exists(Path.Combine(fixture.Target, "recovery")).Should().BeFalse();

        fixture.Generate(config);

        var backupPath = Path.Combine(fixture.Target, "recovery", "alexandria-config.legacy.json");
        File.ReadAllText(backupPath).Should().Be(legacy);
        AssertMode(backupPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.ReadAllText(metadataPath).Should().NotContain("Credentials").And.NotContain("Synthetic1!");
        EnvGenerator.Parse(File.ReadAllText(Path.Combine(fixture.Target, ".env")))["DB_PASSWORD"]
            .Should().Be(config.EnvironmentValues["DB_PASSWORD"]);
    }

    [Fact]
    public void Resolve_interruptedLegacyMigrationBeforeEnvironmentCommit_recoversTheOnlyCredentialCopy()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        config.VolumeNames = new Dictionary<string, string>
        {
            ["postgres_local_data"] = "legacy_postgres",
            ["rabbitmq_local_data"] = "legacy_rabbitmq",
            ["garage_local_meta_data"] = "legacy_garage_meta",
            ["garage_local_storage_data"] = "legacy_garage_data",
        };
        Directory.CreateDirectory(fixture.Target);
        var legacy = JsonSerializer.Serialize(new
        {
            Features = config.Features, Ports = config.Ports, InstallPath = config.InstallPath,
            SourceRoot = config.SourceRoot, Credentials = config.Credentials,
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(fixture.Target, "alexandria-config.json"), legacy);
        fixture.Service.BeginGeneration(config, fixture.Target);
        var inspection = InstallationConfigurationResolver.Inspect(fixture.Context);
        inspection.IsLegacy.Should().BeTrue();
        fixture.Context.Config = inspection.SavedConfiguration!;
        fixture.Credentials.ClearReceivedCalls();

        var recovered = InstallationConfigurationResolver.Resolve(fixture.Context, fixture.Credentials,
            new LegacyInstallationIdentity(config.ProjectName, config.VolumeNames));
        fixture.Generate(recovered);

        recovered.EnvironmentValues["DB_PASSWORD"].Should().Be(config.EnvironmentValues["DB_PASSWORD"]);
        recovered.VolumeNames.Should().BeEquivalentTo(config.VolumeNames);
        fixture.Credentials.DidNotReceive().GenerateAllCredentials(Arg.Any<FeatureSelection>());
        File.ReadAllText(Path.Combine(fixture.Target, "recovery", "alexandria-config.legacy.json")).Should().Be(legacy);
    }

    [Fact]
    public void BeginGeneration_artifactParentIsAFile_stopsBeforeAnyGenerationMutation()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        Directory.CreateDirectory(fixture.Target);
        File.WriteAllText(Path.Combine(fixture.Target, "rabbitmq"), "preserve-existing-file");
        var generation = config.GenerationId;

        var action = () => fixture.Service.BeginGeneration(config, fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*is a file*");
        Directory.GetFiles(fixture.Target).Should().ContainSingle();
        config.GenerationId.Should().Be(generation);
    }

    [Fact]
    public void Generate_specialSourcePath_usesStructuredQuotedBindSources()
    {
        var sourceFolder = OperatingSystem.IsWindows() ? "source '$VALUE # unicode-é" : "source '$VALUE # : unicode-é";
        using var fixture = new ConfigurationArtifactFixture(sourceFolder: sourceFolder);
        var config = fixture.Resolve();

        fixture.Generate(config);

        var compose = File.ReadAllText(Path.Combine(fixture.Target, "docker-compose.yml"));
        var source = config.SourceRoot.Replace("$", "$$").Replace("'", "''");
        compose.Should().Contain("context: '" + source + "/Backend'");
        compose.Should().Contain("source: '" + source + "/scripts/postgres/init'");
        compose.Should().Contain("target: '/docker-entrypoint-initdb.d'");
        compose.Should().Contain("read_only: true");
    }

    private static readonly (string User, string Password)[] BrokerConsumers =
    [
        ("RABBITMQ_USER", "RABBITMQ_PASSWORD"), ("API_RABBITMQ_USER", "API_RABBITMQ_PASSWORD"),
        ("RABBITMQ_DOCUMENT_WORKER", "DOCUMENT_WORKER_RABBITMQ_PASSWORD"), ("RABBITMQ_MEDIA_WORKER", "MEDIA_WORKER_RABBITMQ_PASSWORD"),
        ("RABBITMQ_TRANSPILATION_WORKER", "TRANSPILATION_WORKER_RABBITMQ_PASSWORD"), ("RABBITMQ_LYRICS_WORKER", "LYRICS_WORKER_RABBITMQ_PASSWORD"),
        ("RABBITMQ_MEDIA_METADATA_WORKER", "MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD"), ("RABBITMQ_PLAYLIST_WORKER", "PLAYLIST_WORKER_RABBITMQ_PASSWORD"),
        ("RABBITMQ_ESSENTIA_WORKER", "ESSENTIA_WORKER_RABBITMQ_PASSWORD"),
    ];

    private static void AssertMode(string path, UnixFileMode expected)
    {
        if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(path).Should().Be(expected);
    }
}
