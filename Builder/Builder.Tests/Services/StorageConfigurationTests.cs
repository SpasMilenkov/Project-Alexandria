using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Services;

public sealed class StorageConfigurationTests : IDisposable
{
    private readonly string _target = Path.Combine(Path.GetTempPath(), "builder-storage-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_freshDefault_initializesTenDecimalGb()
    {
        var result = StorageConfigurationResolver.Resolve(new StorageSettings(), null, new Dictionary<string, string>(), repair: false);
        var values = new Dictionary<string, string>();

        StorageConfigurationResolver.ApplyEnvironment(result, values);

        result.GarageCapacityGb.Should().Be(10);
        values["GARAGE_CAPACITY"].Should().Be("10G");
        values["GARAGE_CAPACITY_CHANGE_REQUESTED"].Should().Be("false");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void Resolve_invalidCapacity_rejects(long gb)
    {
        var action = () => StorageConfigurationResolver.Resolve(new StorageSettings { GarageCapacityGb = gb }, null,
            new Dictionary<string, string>(), repair: false);

        action.Should().Throw<InvalidOperationException>().WithMessage("*at least 1 GB*");
    }

    [Fact]
    public void Resolve_legacyMetadataWithoutStorage_preservesExistingEnvCapacity()
    {
        var legacy = JsonSerializer.Deserialize("""{"Features":{"Enabled":[]},"Ports":{}}""",
            AlexandriaJsonContext.Default.InstallationConfig)!;
        var environment = new Dictionary<string, string> { ["GARAGE_CAPACITY"] = "25G" };

        var result = StorageConfigurationResolver.Resolve(new StorageSettings(), legacy.Storage, environment, repair: true);
        StorageConfigurationResolver.ApplyEnvironment(result, environment);

        result.GarageCapacityGb.Should().Be(25);
        result.PreserveExistingCapacity.Should().BeTrue();
        environment["GARAGE_CAPACITY"].Should().Be("25G");
        environment["GARAGE_CAPACITY_CHANGE_REQUESTED"].Should().Be("false");
    }

    [Fact]
    public void Resolve_unrecordedExistingLayout_doesNotPretendItsCapacityIsTenGb()
    {
        var result = StorageConfigurationResolver.Resolve(new StorageSettings(), new StorageSettings(),
            new Dictionary<string, string>(), repair: true);
        var environment = new Dictionary<string, string> { ["GARAGE_CAPACITY"] = "10G" };

        StorageConfigurationResolver.ApplyEnvironment(result, environment);

        environment["GARAGE_CAPACITY"].Should().BeEmpty();
        result.ActualGarageCapacityBytes.Should().BeNull();
        StorageSummary.Describe(new InstallationConfig { Storage = result }, new SystemResources())
            .Should().Contain("Garage layout: existing capacity will be read and preserved during initialization.");
    }

    [Fact]
    public void Resolve_fullRepairWithMissingLegacyStorageAndCapacity_preservesUnknownActualLayout()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var initial = fixture.Resolve();
        fixture.Generate(initial);
        fixture.Service.SaveConfiguration(initial, fixture.Target);
        var path = Path.Combine(fixture.Target, ".env");
        var environment = EnvGenerator.Parse(File.ReadAllText(path));
        environment.Remove("GARAGE_CAPACITY");
        environment.Remove("GARAGE_CAPACITY_CHANGE_REQUESTED");
        ProtectedFile.WriteAtomic(path, EnvGenerator.Render(environment));
        var metadataPath = Path.Combine(fixture.Target, "alexandria-config.json");
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))!.AsObject();
        metadata.Remove("Storage");
        File.WriteAllText(metadataPath, metadata.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var resumed = new InstallationContext { Config = fixture.Service.LoadConfiguration(fixture.Target)! };
        var originalEnvironment = File.ReadAllText(path);

        var result = InstallationConfigurationResolver.Resolve(resumed, fixture.Credentials);

        result.Storage.ActualGarageCapacityBytes.Should().BeNull();
        result.Storage.PreserveExistingCapacity.Should().BeTrue();
        result.EnvironmentValues["GARAGE_CAPACITY"].Should().BeEmpty();
        result.EnvironmentValues["GARAGE_CAPACITY_CHANGE_REQUESTED"].Should().Be("false");
        File.ReadAllText(path).Should().Be(originalEnvironment);
    }

    [Fact]
    public void Resolve_staleLegacyDefault_actualLayoutCapacityWinsWithoutRequestingChange()
    {
        var environment = new Dictionary<string, string> { ["GARAGE_CAPACITY"] = "10G" };
        var saved = new StorageSettings { GarageCapacityGb = 10, ActualGarageCapacityBytes = 25_000_000_000 };

        var result = StorageConfigurationResolver.Resolve(new StorageSettings(), saved, environment, repair: true);
        StorageConfigurationResolver.ApplyEnvironment(result, environment);

        result.GarageCapacityGb.Should().Be(25);
        environment["GARAGE_CAPACITY"].Should().Be("25G");
        environment["GARAGE_CAPACITY_CHANGE_REQUESTED"].Should().Be("false");
    }

    [Fact]
    public void Resolve_reviewedCapacityChange_rejectsBeforeRewritingExistingArtifacts()
    {
        using var fixture = new ConfigurationArtifactFixture();
        fixture.Context.Config.Storage = new StorageSettings { GarageCapacityGb = 25, CapacityReviewed = true };
        var config = fixture.Resolve();
        fixture.Generate(config);
        fixture.Service.SaveConfiguration(config, fixture.Target);
        var environmentBefore = File.ReadAllText(Path.Combine(fixture.Target, ".env"));
        var metadataBefore = File.ReadAllText(Path.Combine(fixture.Target, "alexandria-config.json"));
        fixture.Context.Config.Storage = new StorageSettings { GarageCapacityGb = 30, CapacityReviewed = true };

        var action = () => fixture.Resolve();

        action.Should().Throw<InvalidOperationException>().WithMessage("*existing Garage capacity*25000000000*preserved*");
        File.ReadAllText(Path.Combine(fixture.Target, ".env")).Should().Be(environmentBefore);
        File.ReadAllText(Path.Combine(fixture.Target, "alexandria-config.json")).Should().Be(metadataBefore);
    }

    [Fact]
    public void Commit_actualReceipt_updatesConfigurationAndPreservesNonsecretState()
    {
        Directory.CreateDirectory(Path.Combine(_target, "init-output"));
        File.WriteAllText(Path.Combine(_target, "init-output", StorageCapacityReceipt.FileName), "25000000000\n");
        var context = new InstallationContext { InstallPath = _target };

        StorageCapacityReceipt.Commit(context).Should().Be(25_000_000_000);

        context.Config.Storage.ActualGarageCapacityBytes.Should().Be(25_000_000_000);
        context.Config.Storage.GarageCapacityGb.Should().Be(25);
        context.Config.EnvironmentValues["GARAGE_CAPACITY"].Should().Be("25G");
        context.Config.EnvironmentValues["GARAGE_CAPACITY_CHANGE_REQUESTED"].Should().Be("false");
        var json = JsonSerializer.Serialize(context.Config, AlexandriaJsonContext.Default.InstallationConfig);
        var restored = JsonSerializer.Deserialize(json, AlexandriaJsonContext.Default.InstallationConfig)!;
        restored.Storage.ActualGarageCapacityBytes.Should().Be(25_000_000_000);
        json.Should().NotContain("CapacityReviewed").And.NotContain("PreserveExistingCapacity");
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1G")]
    [InlineData("1000000000\n25000000000")]
    public void Read_invalidActualReceipt_rejects(string content)
    {
        Directory.CreateDirectory(Path.Combine(_target, "init-output"));
        File.WriteAllText(Path.Combine(_target, "init-output", StorageCapacityReceipt.FileName), content);

        var action = () => StorageCapacityReceipt.Read(_target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*valid actual layout capacity*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_target)) Directory.Delete(_target, recursive: true);
    }
}
