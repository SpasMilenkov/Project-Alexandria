using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public sealed class InstallationModelPreparationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-model-opt-in", Guid.NewGuid().ToString("N"));
    private static readonly byte[] Payload = "synthetic-pinned-model"u8.ToArray();

    private InstallationConfig CreateConfig(bool allowDownloads)
    {
        var assets = Path.Combine(_root, "source", "classificators", "essentia");
        var models = Path.Combine(assets, "models");
        Directory.CreateDirectory(models);
        File.WriteAllText(Path.Combine(models, "models.sha256"), Convert.ToHexString(SHA256.HashData(Payload)) + "  common.pb\n");
        File.WriteAllText(Path.Combine(assets, "model-requirements.tsv"), $"shared common.pb {Payload.Length} test/common.pb\n");
        var config = new InstallationConfig
        {
            SourceRoot = Path.Combine(_root, "source"),
            InstallPath = Path.Combine(_root, "settings"),
            AllowModelDownloads = allowDownloads,
        };
        config.Features.SetAudioBackbone("effnet");
        config.Features.SetEnabled(FeatureCatalog.Autotagging.Id, true);
        return config;
    }

    [Fact]
    public void Prepare_offlineMissingAssets_failsWithoutRequestingHttp()
    {
        var config = CreateConfig(allowDownloads: false);
        using var handler = new ModelHandler();
        using var client = new HttpClient(handler);
        var service = new InstallationModelPreparation(client);

        var prepare = () => service.Prepare(config, _ => { }, TestContext.Current.CancellationToken);

        prepare.Should().Throw<FileNotFoundException>().WithMessage("*downloads are disabled*");
        handler.Requests.Should().Be(0);
    }

    [Fact]
    public void Prepare_explicitDownloadFetchesPinnedAsset_thenReusesCacheOffline()
    {
        var config = CreateConfig(allowDownloads: true);
        using var handler = new ModelHandler();
        using var client = new HttpClient(handler);
        var service = new InstallationModelPreparation(client);
        var output = new List<string>();

        service.Prepare(config, output.Add, TestContext.Current.CancellationToken);
        config.AllowModelDownloads = false;
        service.Prepare(config, output.Add, TestContext.Current.CancellationToken);

        handler.Requests.Should().Be(1);
        File.ReadAllBytes(Path.Combine(config.InstallPath, "models", "common.pb")).Should().Equal(Payload);
        output.Should().Contain(line => line.Contains("Downloading model"))
            .And.Contain(line => line.Contains("Reused verified cache"));
    }

    [Fact]
    public void Prepare_analysisOff_withConsentDoesNotCreateCacheOrUseHttp()
    {
        var config = CreateConfig(allowDownloads: true);
        config.Features.SetAudioBackbone("off");
        using var handler = new ModelHandler();
        using var client = new HttpClient(handler);

        new InstallationModelPreparation(client).Prepare(config, _ => { }, TestContext.Current.CancellationToken);

        handler.Requests.Should().Be(0);
        Directory.Exists(config.InstallPath).Should().BeFalse();
    }

    [Fact]
    public void Resolve_persistsReviewedConsentAcrossGenerationAndRepair()
    {
        using var fixture = new ConfigurationArtifactFixture();
        fixture.Context.Config.AllowModelDownloads = true;

        var config = fixture.Resolve();
        fixture.Generate(config);
        var inspected = InstallationConfigurationResolver.Inspect(fixture.Context);

        config.AllowModelDownloads.Should().BeTrue();
        inspected.SavedConfiguration!.AllowModelDownloads.Should().BeTrue();
        inspected.IsIncompleteGeneration.Should().BeFalse();
        fixture.Context.Config = inspected.SavedConfiguration;
        fixture.Context.Config.AllowModelDownloads = false;
        fixture.Resolve().AllowModelDownloads.Should().BeFalse();
    }

    private sealed class ModelHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            request.RequestUri!.AbsoluteUri.Should().Be("https://essentia.upf.edu/models/test/common.pb");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
