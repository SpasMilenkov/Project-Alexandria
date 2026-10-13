using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Integration;

public sealed class SourceImageFixtureTests : IDisposable
{
    private static readonly string ImageId = "sha256:" + new string('a', 64);
    private readonly string _manifest = Path.Combine(Path.GetTempPath(), "builder-source-images-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_successfulBuildManifest_usesImmutableIdsForSelectedServices()
    {
        var fixture = Load("[" + Entry("api") + "," + Entry("essentia") + "]");

        fixture.Image("api").Should().Be(ImageId);
        fixture.Image("essentia-effnet-worker").Should().Be(ImageId);
        fixture.Image("essentia-maest-worker").Should().Be(ImageId);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"name\":\"api\"}]")]
    [InlineData("[{\"name\":\"unknown\",\"image\":\"source:local\",\"image_id\":\"<id>\",\"exit_code\":0}]")]
    [InlineData("[{\"name\":\"api\",\"image\":\"source:local\",\"image_id\":\"<id>\",\"exit_code\":1}]")]
    [InlineData("[{\"name\":\"api\",\"image\":\"source:local\",\"image_id\":\"<id>\",\"exit_code\":\"0\"}]")]
    [InlineData("[{\"name\":\"api\",\"image\":\"source:local\",\"image_id\":\"source:local\",\"exit_code\":0}]")]
    [InlineData("[{\"name\":\"api\",\"image\":\"source:local;false\",\"image_id\":\"<id>\",\"exit_code\":0}]")]
    [InlineData("[{\"name\":\"api\",\"name\":\"api\",\"image\":\"source:local\",\"image_id\":\"<id>\",\"exit_code\":0}]")]
    public void Load_invalidBuildResult_rejectsManifest(string json)
    {
        Action load = () => Load(json.Replace("<id>", ImageId, StringComparison.Ordinal));

        load.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Load_duplicateService_rejectsManifest()
    {
        Action load = () => Load("[" + Entry("api") + "," + Entry("api") + "]");

        load.Should().Throw<InvalidOperationException>().WithMessage("*duplicate service*");
    }

    [Fact]
    public void Load_inlineJson_requiresExistingManifestFile()
    {
        Action load = () => SourceImageFixture.Load("[" + Entry("api") + "]");

        load.Should().Throw<InvalidOperationException>().WithMessage("*existing JSON build-results file*");
    }

    [Fact]
    public void ValidateImages_selectedServiceMissing_failsBeforeDockerWithoutFallback()
    {
        var fixture = Load("[" + Entry("api") + "]");
        var dockerCalls = 0;
        Action validate = () => fixture.ValidateImages(["playlist-worker"], _ =>
        {
            dockerCalls++;
            return new ProcessResult(0, ImageId, string.Empty);
        });

        validate.Should().Throw<InvalidOperationException>().WithMessage("*missing selected service*never falls back*");
        dockerCalls.Should().Be(0);
    }

    [Fact]
    public void ValidateImages_matchingNamedImage_onlyInspectsRecordedName()
    {
        var fixture = Load("[" + Entry("api") + "]");
        var calls = new List<string[]>();

        fixture.ValidateImages(["api", "api"], arguments =>
        {
            calls.Add(arguments.ToArray());
            return new ProcessResult(0, ImageId + "\n", string.Empty);
        });

        calls.Should().ContainSingle().Which.Should().Equal("image", "inspect", "--format", "{{.Id}}", "source-api:local");
    }

    [Theory]
    [InlineData(0, "sha256:changed")]
    [InlineData(1, "")]
    public void ValidateImages_changedOrUnavailableNamedImage_failsWithoutFallback(int exitCode, string inspectedId)
    {
        var fixture = Load("[" + Entry("api") + "]");
        Action validate = () => fixture.ValidateImages(["api"], _ => new ProcessResult(exitCode, inspectedId, string.Empty));

        validate.Should().Throw<InvalidOperationException>().WithMessage("*no longer matches*never falls back*");
    }

    [Fact]
    public void AppendOverride_sourceService_preservesImageEntrypointAndFiles()
    {
        var fixture = Load("[" + Entry("api") + "]");
        var text = new StringBuilder();

        fixture.AppendOverride(text, "api");

        text.ToString().Should().Contain("image: " + ImageId).And.Contain("build: !reset null")
            .And.Contain("pull_policy: never").And.NotContain("entrypoint").And.NotContain("volumes");
    }

    [Theory]
    [InlineData("/app")]
    [InlineData("/app/worker.py")]
    [InlineData("/app/models.sha256")]
    [InlineData("/scripts/init.sh")]
    [InlineData("/usr/share/nginx/html")]
    [InlineData("/fixture-config/appsettings.json")]
    public void ValidateCompose_imageOwnedFileOverlay_rejectsComposition(string target)
    {
        var fixture = Load("[" + Entry("api") + "]");
        using var composition = Composition(new { image = ImageId, volumes = new[] { new { target } } });
        Action validate = () => fixture.ValidateCompose(composition.RootElement, ["api"]);

        validate.Should().Throw<InvalidOperationException>().WithMessage("*cannot overlay image-owned*");
    }

    [Theory]
    [InlineData("entrypoint")]
    [InlineData("command")]
    [InlineData("build")]
    public void ValidateCompose_imageExecutionOverride_rejectsComposition(string property)
    {
        var fixture = Load("[" + Entry("api") + "]");
        using var composition = Composition(new Dictionary<string, object> { ["image"] = ImageId, [property] = "override" });
        Action validate = () => fixture.ValidateCompose(composition.RootElement, ["api"]);

        validate.Should().Throw<InvalidOperationException>().WithMessage("*preserve the image entrypoint*");
    }

    [Fact]
    public void ValidateCompose_normalizedNullExecutionFields_preservesImageDefaults()
    {
        var fixture = Load("[" + Entry("api") + "]");
        using var composition = Composition(new { image = ImageId, entrypoint = (string?)null, command = (string?)null });

        fixture.ValidateCompose(composition.RootElement, ["api"]);
    }

    [Fact]
    public void ValidateCompose_emptyEntrypoint_rejectsClearedImageDefaults()
    {
        var fixture = Load("[" + Entry("api") + "]");
        using var composition = Composition(new { image = ImageId, entrypoint = Array.Empty<string>() });

        var validate = () => fixture.ValidateCompose(composition.RootElement, ["api"]);

        validate.Should().Throw<InvalidOperationException>().WithMessage("*preserve the image entrypoint*");
    }

    [Fact]
    public void ValidateCompose_modelAndGeneratedConfigMounts_acceptsComposition()
    {
        var fixture = Load("[" + Entry("api") + "]");
        using var composition = Composition(new
        {
            image = ImageId,
            volumes = new[] { new { target = "/app/models" }, new { target = "/etc/nginx/nginx.conf" }, new { target = "/data" } },
        });

        fixture.ValidateCompose(composition.RootElement, ["api"]);
    }

    private SourceImageFixture Load(string json)
    {
        File.WriteAllText(_manifest, json);
        return SourceImageFixture.Load(_manifest);
    }

    private static string Entry(string name) => JsonSerializer.Serialize(new
    {
        name,
        image = "source-" + name + ":local",
        image_id = ImageId,
        exit_code = 0,
        seconds = 1,
        log = "/tmp/build.log",
    }, new JsonSerializerOptions());

    private static JsonDocument Composition(object definition) => JsonDocument.Parse(JsonSerializer.Serialize(
        new Dictionary<string, object> { ["api"] = definition }, new JsonSerializerOptions()));

    public void Dispose() => File.Delete(_manifest);
}
