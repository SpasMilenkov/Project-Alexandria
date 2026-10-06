using System.Text.RegularExpressions;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class EnvGeneratorTests
{
    private static readonly CredentialService CredentialSource = new();

    private static AdminAccountInput CreateAdmin() => new()
    {
        Email = "owner@example.com",
        Username = "owner",
        Password = "correct-horse-battery",
    };

    [Fact]
    public void BuildValues_coversEveryRequiredVariableOfGeneratedCompose()
    {
        foreach (var monitoring in new[] { true, false })
        {
            var features = FeatureSelection.FromDefaults();

            features.SetEnabled(FeatureCatalog.Monitoring.Id, monitoring);

            var credentials = CredentialSource.GenerateAllCredentials(features);
            var values = EnvGenerator.BuildValues(features, credentials, CreateAdmin(), null);

            var compose = new ConfigurationService(new TemplateService("Local"))
                .GenerateDockerCompose(new InstallationConfig { Features = features });

            var requiredVars = Regex.Matches(compose, @"\$\{([A-Z_][A-Z0-9_]*):\?")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            requiredVars.Should().NotBeEmpty();

            values.Keys.Should().Contain(requiredVars,
                because: $"every hard-required compose variable must exist in the generated .env (monitoring={monitoring})");
        }
    }

    [Fact]
    public void BuildValues_featureSwitches_mirrorWizardSelection()
    {
        var features = FeatureSelection.FromDefaults();

        features.SetEnabled(FeatureCatalog.Lyrics.Id, false);
        features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, false);

        var values = EnvGenerator.BuildValues(features, new Dictionary<string, string>(), CreateAdmin(), null);

        values["LYRICS_ENABLED"].Should().Be("false");
        values["STREAMING_ENABLED"].Should().Be("false");
        values["AUTOTAGGING_ENABLED"].Should().Be("true");
    }

    [Fact]
    public void BuildValues_garageKeys_pendingUntilMerge()
    {
        var features = new FeatureSelection();

        var values = EnvGenerator.BuildValues(features, new Dictionary<string, string>(), CreateAdmin(), null);

        values["GARAGE_S3_ACCESS_KEY"].Should().Be("placeholder");
        values["GARAGE_S3_WORKER_SECRET_KEY"].Should().Be("placeholder");
    }

    [Fact]
    public void BuildValues_afterGarageMerge_replacesPendingKeysWithRealCredentials()
    {
        var features = new FeatureSelection();
        var garage = new GarageCredentialSet("GK1", "s1", "GK2", "s2", "GK3", "s3");

        var values = EnvGenerator.BuildValues(features, new Dictionary<string, string>(), CreateAdmin(), garage);

        values["GARAGE_S3_ACCESS_KEY"].Should().Be("GK1");
        values["GARAGE_S3_WORKER_ACCESS_KEY"].Should().Be("GK2");
        values["GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY"].Should().Be("s3");
    }

    [Fact]
    public void BuildValues_monitoringOn_includesGrafanaAndExporterEntries()
    {
        var features = new FeatureSelection();

        features.SetEnabled(FeatureCatalog.Monitoring.Id, true);

        var credentials = CredentialSource.GenerateAllCredentials(features);

        var values = EnvGenerator.BuildValues(features, credentials, CreateAdmin(), null);

        values.Should().ContainKey("GRAFANA_PASSWORD");
        values.Should().ContainKey("POSTGRES_EXPORTER_PASSWORD");
    }

    [Fact]
    public void Render_containsAllValueLinesAndNoRawDictionaryNoise()
    {
        var features = new FeatureSelection();
        var credentials = CredentialSource.GenerateAllCredentials(features);
        var values = EnvGenerator.BuildValues(features, credentials, CreateAdmin(), null);

        var rendered = EnvGenerator.Render(values);

        rendered.Should().Contain("DB_PORT=5432");
        rendered.Should().Contain($"ADMIN_EMAIL={CreateAdmin().Email}");
        rendered.Should().Contain("# --- Garage S3 storage ---");
    }

    [Fact]
    public void MergeWithExisting_keepsUserValues_andFillsOnlyMissingOrPending()
    {
        var existing = """
            # user's original file
            DB_PASSWORD=keep-me
            ADMIN_EMAIL=user@custom.domain
            GARAGE_S3_ACCESS_KEY=GK_already_merged
            UNRELATED_VAR=stay
            """;

        var generated = new Dictionary<string, string>
        {
            ["DB_PASSWORD"] = "generated",
            ["ADMIN_EMAIL"] = "generated@example.com",
            ["ADMIN_USERNAME"] = "generated-user",
            ["GARAGE_S3_ACCESS_KEY"] = "placeholder",
            ["UNRELATED_VAR"] = "generated-unrelated",
        };

        var merged = EnvGenerator.MergeWithExisting(generated, existing);

        merged["DB_PASSWORD"].Should().Be("keep-me");
        merged["ADMIN_EMAIL"].Should().Be("user@custom.domain");
        merged["GARAGE_S3_ACCESS_KEY"].Should().Be("GK_already_merged");
        merged["ADMIN_USERNAME"].Should().Be("generated-user");
        // Unknown pre-existing keys are preserved so nothing is silently lost
        merged["UNRELATED_VAR"].Should().Be("stay");
    }
}
