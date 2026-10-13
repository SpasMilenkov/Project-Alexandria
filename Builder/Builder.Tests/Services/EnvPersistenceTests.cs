using System.Data.Common;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class EnvPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "builder-env-tests", Guid.NewGuid().ToString("N"));

    public EnvPersistenceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void MergeRenderWriteRead_preservesCredentialsUnknownValuesAndCommentsWithoutDuplicates()
    {
        var existing = """
            # Keep this custom configuration
            DB_PASSWORD='preserved$PASSWORD #= café'
            CUSTOM_SETTING='  unknown = # $VALUE  ' # explanation
            INTENTIONALLY_EMPTY=''
            GARAGE_S3_ACCESS_KEY=placeholder
            GARAGE_S3_SECRET_KEY='placeholder'
            GARAGE_S3_WORKER_ACCESS_KEY=already-established
            """;

        var generated = new Dictionary<string, string>
        {
            ["DB_PASSWORD"] = "fresh-must-not-win",
            ["GARAGE_S3_ACCESS_KEY"] = "GK_authoritative",
            ["GARAGE_S3_SECRET_KEY"] = "authoritative$secret",
            ["GARAGE_S3_WORKER_ACCESS_KEY"] = "different-must-not-win",
            ["GARAGE_S3_PUBLIC_ENDPOINT"] = "https://host/s3",
            ["LYRICS_WORKER_RABBITMQ_PASSWORD"] = "lyrics-value",
            ["INTENTIONALLY_EMPTY"] = "generated-default",
        };

        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, existing);
        var merged = EnvGenerator.MergeWithExisting(generated, existing);

        EnvGenerator.WriteFile(path, merged);

        var content = File.ReadAllText(path);
        var persisted = EnvGenerator.Parse(content);
        persisted.Should().BeEquivalentTo(merged);
        persisted["DB_PASSWORD"].Should().Be("preserved$PASSWORD #= café");
        persisted["CUSTOM_SETTING"].Should().Be("  unknown = # $VALUE  ");
        persisted["INTENTIONALLY_EMPTY"].Should().BeEmpty();
        persisted["GARAGE_S3_ACCESS_KEY"].Should().Be("GK_authoritative");
        persisted["GARAGE_S3_SECRET_KEY"].Should().Be("authoritative$secret");
        persisted["GARAGE_S3_WORKER_ACCESS_KEY"].Should().Be("already-established");
        content.Should().Contain("# Keep this custom configuration");
        content.Should().Contain("# explanation");
        content.Split('\n').Count(line => line.StartsWith("GARAGE_S3_PUBLIC_ENDPOINT=", StringComparison.Ordinal)).Should().Be(1);
        content.Split('\n').Count(line => line.StartsWith("LYRICS_WORKER_RABBITMQ_PASSWORD=", StringComparison.Ordinal)).Should().Be(1);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData(" spaces # = $HOME ${OTHER} ")]
    [InlineData("it's quoted")]
    [InlineData("double\"quoted")]
    [InlineData("ordinary\\path")]
    [InlineData("trailing\\")]
    [InlineData("slash\\'quote")]
    [InlineData("two\\\\'quote")]
    [InlineData("cash$HOME and quote\" trailing\\")]
    [InlineData("café 文件 😀")]
    public void Render_specialValues_roundTripThroughLiteralParser(string value)
    {
        var rendered = EnvGenerator.Render(new Dictionary<string, string> { ["VALUE"] = value });

        EnvGenerator.Parse(rendered)["VALUE"].Should().Be(value);
    }

    [Fact]
    public void WriteFile_repeatedRepairWithoutSettingChanges_preservesCanonicalFileExactly()
    {
        var path = Path.Combine(_directory, ".env");
        var values = new Dictionary<string, string> { ["DB_PASSWORD"] = "synthetic $/# café", ["CUSTOM_SETTING"] = "retained" };
        EnvGenerator.WriteFile(path, values);
        var original = File.ReadAllText(path);

        for (var attempt = 0; attempt < 4; attempt++) EnvGenerator.WriteFile(path, values);

        File.ReadAllText(path).Should().Be(original);
    }

    [Theory]
    [InlineData("BAD-NAME=private-value")]
    [InlineData("UNSET_VARIABLE")]
    [InlineData("VALUE=${EXTERNAL}")]
    [InlineData("VALUE=\"$EXTERNAL\"")]
    [InlineData("VALUE='one\ntwo'")]
    [InlineData("VALUE=one\nVALUE=two")]
    [InlineData("VALUE=\"unsupported\\q\"")]
    [InlineData("VALUE='closed' unexpected")]
    public void Parse_unsupportedSyntax_stopsWithRedactedLineDiagnostic(string existing)
    {
        var action = () => EnvGenerator.Parse(existing);

        var exception = action.Should().Throw<FormatException>().Which;
        exception.Message.Should().Contain("line");
        exception.Message.Should().NotContain("private-value");
        exception.Message.Should().NotContain("EXTERNAL");
        exception.Message.Should().NotContain("unexpected");
    }

    [Theory]
    [InlineData("line\nbreak")]
    [InlineData("carriage\rreturn")]
    [InlineData("tab\tvalue")]
    [InlineData("nul\0value")]
    public void WriteFile_controlValue_preservesExistingArtifact(string invalid)
    {
        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, "DB_PASSWORD=retained\n");

        var action = () => EnvGenerator.WriteFile(path, new Dictionary<string, string> { ["DB_PASSWORD"] = invalid });

        action.Should().Throw<FormatException>();
        File.ReadAllText(path).Should().Be("DB_PASSWORD=retained\n");
        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public void WriteFile_unsupportedExistingFile_doesNotReplaceIt()
    {
        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, "DB_PASSWORD=${PASSWORD_FROM_PROCESS}\n");

        var action = () => EnvGenerator.WriteFile(path, new Dictionary<string, string> { ["DB_PASSWORD"] = "generated" });

        action.Should().Throw<FormatException>();
        File.ReadAllText(path).Should().Be("DB_PASSWORD=${PASSWORD_FROM_PROCESS}\n");
    }

    [Fact]
    public void Render_identicalLegacyDuplicates_preservesOneAssignmentAndComments()
    {
        var existing = "GARAGE_S3_PUBLIC_ENDPOINT=https://host/s3\nGARAGE_S3_PUBLIC_ENDPOINT='https://host/s3' # legacy duplicate\n";

        var rendered = EnvGenerator.Render(EnvGenerator.Parse(existing), existing);

        rendered.Split('\n').Count(line => line.StartsWith("GARAGE_S3_PUBLIC_ENDPOINT=", StringComparison.Ordinal)).Should().Be(1);
        rendered.Should().Contain("# legacy duplicate");
    }

    [Fact]
    public void BuildValues_canonicalMonitoringAliases_takePriorityOverLegacyAliases()
    {
        var features = new FeatureSelection();
        features.SetEnabled(FeatureCatalog.Monitoring.Id, true);
        var credentials = new Dictionary<string, string>
        {
            ["GARAGE_S3_METRICS_TOKEN"] = "canonical-token",
            ["GARAGE_METRICS_TOKEN"] = "legacy-token",
            ["GRAFANA_PASSWORD"] = "canonical-password",
            ["GRAFANA_ADMIN_PASSWORD"] = "legacy-password",
        };

        var values = EnvGenerator.BuildValues(features, credentials, new AdminAccountInput(), null);

        values["GARAGE_S3_METRICS_TOKEN"].Should().Be("canonical-token");
        values["GRAFANA_PASSWORD"].Should().Be("canonical-password");
    }

    [Fact]
    public void BuildValues_playlistBrokerCredentials_arePresentWithoutOptionalFeatures()
    {
        var features = new FeatureSelection();
        var credentials = new CredentialService().GenerateAllCredentials(features);

        var values = EnvGenerator.BuildValues(features, credentials, new AdminAccountInput(), null);

        values["RABBITMQ_PLAYLIST_WORKER"].Should().Be("playlist_worker_user");
        values["PLAYLIST_WORKER_RABBITMQ_PASSWORD"].Should().Be(credentials["PLAYLIST_WORKER_RABBITMQ_PASSWORD"]);
        values["RABBITMQ_LYRICS_WORKER"].Should().Be("lyrics_worker_user");
    }

    [Fact]
    public void BuildDatabaseConnectionString_specialCredentials_cannotInjectSettings()
    {
        var values = new Dictionary<string, string>
        {
            ["DB_PORT"] = "5432",
            ["DB_NAME"] = "name;Host=evil",
            ["DB_USERNAME"] = "user\"name",
            ["DB_PASSWORD"] = "a;Host=evil;Password=\"quote'$#= ",
        };

        var rendered = EnvGenerator.BuildDatabaseConnectionString(values);
        var parsed = new DbConnectionStringBuilder { ConnectionString = rendered };

        parsed["Host"].Should().Be("postgres");
        parsed["Database"].Should().Be(values["DB_NAME"]);
        parsed["Username"].Should().Be(values["DB_USERNAME"]);
        parsed["Password"].Should().Be(values["DB_PASSWORD"]);
    }

    [Fact]
    public void BuildAmqpUri_specialCredentialsAndVirtualHost_areEncodedSeparately()
    {
        var rendered = EnvGenerator.BuildAmqpUri("rabbitmq", "user@/é", "pass:#?@/$=", "/vhost name");

        rendered.Should().Be("amqp://user%40%2F%C3%A9:pass%3A%23%3F%40%2F%24%3D@rabbitmq:5672/%2Fvhost%20name");
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
