using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public sealed class ModelPreparationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-model-tests", Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly string _target;
    private readonly ModelPreparationService _service = new();

    public ModelPreparationServiceTests()
    {
        _source = Path.Combine(_root, "source");
        _target = Path.Combine(_root, "target");
        Directory.CreateDirectory(Path.Combine(_source, "classificators", "essentia", "models"));
    }

    [Theory]
    [InlineData("effnet", 24056930L, "genre_discogs400-discogs-effnet-1.pb")]
    [InlineData("maest", 370003909L, "discogs-maest-30s-pw-519l-2.pb")]
    public void CreatePlan_repositoryBackbone_hasOnlyActualSelectedInferenceDependencies(string backbone, long bytes, string genreModel)
    {
        var repository = ConfigurationArtifactFixture.FindRepositoryRoot();

        var plan = _service.CreatePlan(repository, _target, backbone);

        plan.Assets.Should().HaveCount(17);
        plan.Assets.Select(asset => asset.FileName).Should().Contain("discogs-effnet-bs64-1.pb").And.Contain(genreModel);
        plan.Assets.Select(asset => asset.FileName).Should().NotContain("discogs-maest-30s-pw-ts-2.pb")
            .And.NotContain("genre_discogs519-discogs-maest-30s-pw-519l-1.pb")
            .And.NotContain("discogs-effnet-bs64-1.json");
        plan.ExpectedBytes.Should().Be(bytes);
        plan.ModelDirectory.Should().Be(Path.Combine(_target, "models"));
    }

    [Fact]
    public async Task PrepareAsync_analysisOff_doesNotReadAssetsCreateCacheOrContactSource()
    {
        var result = await _service.PrepareAsync("/missing-source", _target, "off", cancellationToken: TestContext.Current.CancellationToken);

        result.VerifiedBytes.Should().Be(0);
        Directory.Exists(_target).Should().BeFalse();
    }

    [Fact]
    public async Task PrepareAsync_existingLocalSource_copiesVerifiesAndReusesCacheWithoutSource()
    {
        var plan = CreateFixture();
        var progress = new List<ModelPreparationProgress>();

        var first = await _service.PrepareAsync(plan, progress.Add, TestContext.Current.CancellationToken);
        Directory.Delete(plan.SourceDirectory, recursive: true);
        var second = await _service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        first.PreparedFiles.Should().Be(2);
        first.VerifiedBytes.Should().Be(plan.ExpectedBytes);
        second.ReusedFiles.Should().Be(2);
        second.PreparedFiles.Should().Be(0);
        progress.Should().Contain(item => item.Stage == "Copying existing model")
            .And.Contain(item => item.Stage == "Verifying prepared model");
        progress.Last().CompletedBytes.Should().Be(plan.ExpectedBytes);
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
        File.ReadAllLines(Path.Combine(plan.ModelDirectory, "models.sha256")).Should().HaveCount(2);

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(plan.ModelDirectory).Should().Be((UnixFileMode)0x1ed);
            File.GetUnixFileMode(Path.Combine(plan.ModelDirectory, "common.pb")).Should().Be((UnixFileMode)0x1a4);
        }
    }

    [Fact]
    public async Task PrepareAsync_corruptCache_replacesOnlyAfterLocalVerification()
    {
        var plan = CreateFixture();
        Directory.CreateDirectory(plan.ModelDirectory);
        var target = Path.Combine(plan.ModelDirectory, "common.pb");
        File.WriteAllText(target, "corrupt");

        await _service.PrepareAsync(plan, progress =>
        {
            if (progress.FileName == "common.pb" && progress.Stage == "Verifying prepared model")
            {
                File.ReadAllText(target).Should().Be("corrupt");
            }
        }, TestContext.Current.CancellationToken);

        File.ReadAllText(target).Should().Be("fixture-common");
    }

    [Fact]
    public async Task PrepareAsync_corruptLocalSource_rejectsPromotionPreservesVerifiedFilesAndCleansTemporary()
    {
        var plan = CreateFixture();
        Directory.CreateDirectory(plan.ModelDirectory);
        File.Copy(Path.Combine(plan.SourceDirectory, "common.pb"), Path.Combine(plan.ModelDirectory, "common.pb"));
        File.WriteAllText(Path.Combine(plan.SourceDirectory, "genre.json"), "wrong--genre");
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*verification failed*genre.json*downloads are disabled*");
        handler.Requests.Should().Be(0);
        File.ReadAllText(Path.Combine(plan.ModelDirectory, "common.pb")).Should().Be("fixture-common");
        File.Exists(Path.Combine(plan.ModelDirectory, "genre.json")).Should().BeFalse();
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_missingModels_downloadsDisabledByDefaultEvenWithHttpClient()
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<FileNotFoundException>().WithMessage("*downloads are disabled*");
        handler.Requests.Should().Be(0);
    }

    [Fact]
    public async Task PrepareAsync_cancelledCopy_keepsCompletedCacheAndRemovesPartialFile()
    {
        var plan = CreateFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var action = () => _service.PrepareAsync(plan, progress =>
        {
            if (progress.FileName == "genre.json" && progress.Stage == "Copying existing model" && progress.CompletedBytes > 0)
            {
                cancellation.Cancel();
            }
        }, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(Path.Combine(plan.ModelDirectory, "common.pb")).Should().BeTrue();
        File.Exists(Path.Combine(plan.ModelDirectory, "genre.json")).Should().BeFalse();
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();

        var resumed = await _service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);
        resumed.ReusedFiles.Should().Be(1);
        resumed.PreparedFiles.Should().Be(1);
    }

    [Fact]
    public async Task PrepareAsync_explicitFetchWithVerifiedCache_reusesWithoutContactingSource()
    {
        var plan = CreateFixture();
        await _service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);
        Directory.Delete(plan.SourceDirectory, recursive: true);
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true);

        var result = await service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        result.ReusedFiles.Should().Be(2);
        result.PreparedFiles.Should().Be(0);
        result.VerifiedBytes.Should().Be(plan.ExpectedBytes);
        handler.Requests.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrepareAsync_explicitFetchWithCorruptLocalSource_recoversCacheAndPreservesVerifiedModels(bool useCacheAsSource)
    {
        var plan = CreateFixture();
        Directory.CreateDirectory(plan.ModelDirectory);
        var target = Path.Combine(plan.ModelDirectory, "common.pb");
        var verifiedTarget = Path.Combine(plan.ModelDirectory, "genre.json");
        File.WriteAllText(target, "corrupt-cache");
        File.Copy(Path.Combine(plan.SourceDirectory, "genre.json"), verifiedTarget);
        File.WriteAllText(Path.Combine(plan.SourceDirectory, "common.pb"), "corrupt-common");

        if (useCacheAsSource)
        {
            plan = plan with { SourceDirectory = plan.ModelDirectory };
        }

        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("fixture-common")
        });
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true);
        var progress = new List<ModelPreparationProgress>();

        var result = await service.PrepareAsync(plan, item =>
        {
            progress.Add(item);

            if (item.FileName == "common.pb" && item.Stage == "Verifying prepared model")
            {
                File.ReadAllText(target).Should().Be("corrupt-cache");
                File.ReadAllText(verifiedTarget).Should().Be("fixture-genre");
            }
        }, TestContext.Current.CancellationToken);

        result.ReusedFiles.Should().Be(1);
        result.PreparedFiles.Should().Be(1);
        result.VerifiedBytes.Should().Be(plan.ExpectedBytes);
        handler.Requests.Should().Be(1);
        handler.LastUri!.AbsoluteUri.Should().Be("https://essentia.upf.edu/models/fixture/common.pb");
        File.ReadAllText(target).Should().Be("fixture-common");
        File.ReadAllText(verifiedTarget).Should().Be("fixture-genre");
        progress.Should().Contain(item => item.Stage == "Downloading model")
            .And.NotContain(item => item.Stage == "Copying existing model");
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task PrepareAsync_explicitFetchWithTransientFailures_retriesWithinBoundWithoutNetwork(HttpStatusCode status)
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        var handler = new FixtureHandler(attempt => attempt < 3
            ? new HttpResponseMessage(status)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fixture-common") });
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var result = await service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        handler.Requests.Should().Be(ModelPreparationService.MaximumAttempts);
        handler.LastUri!.AbsoluteUri.Should().Be("https://essentia.upf.edu/models/fixture/common.pb");
        result.PreparedFiles.Should().Be(2);
    }

    [Fact]
    public async Task PrepareAsync_explicitFetchRepeatedFailure_stopsAfterBoundAndCleansTemporary()
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HttpRequestException>();
        handler.Requests.Should().Be(ModelPreparationService.MaximumAttempts);
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Theory]
    [InlineData("fixture-common-extra", "*exceeds its pinned size*")]
    [InlineData("too-short", "*verification failed*")]
    [InlineData("corrupt-common", "*verification failed*")]
    public async Task PrepareAsync_explicitFetchWithInvalidPayload_rejectsWithoutRetryOrReplacingCache(string payload, string message)
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        Directory.CreateDirectory(plan.ModelDirectory);
        var target = Path.Combine(plan.ModelDirectory, "common.pb");
        var verifiedTarget = Path.Combine(plan.ModelDirectory, "genre.json");
        File.WriteAllText(target, "corrupt-cache");
        File.Copy(Path.Combine(plan.SourceDirectory, "genre.json"), verifiedTarget);
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload)
        });
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage(message);
        handler.Requests.Should().Be(1);
        File.ReadAllText(target).Should().Be("corrupt-cache");
        File.ReadAllText(verifiedTarget).Should().Be("fixture-genre");
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_explicitFetchWithRedirect_rejectsWithoutFollowingOrRetrying()
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://untrusted.example/common.pb") },
            Content = new StringContent("fixture-common")
        });
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        var failure = await action.Should().ThrowAsync<HttpRequestException>();
        failure.Which.StatusCode.Should().Be(HttpStatusCode.Found);
        handler.Requests.Should().Be(1);
        handler.LastUri!.AbsoluteUri.Should().Be("https://essentia.upf.edu/models/fixture/common.pb");
        File.Exists(Path.Combine(plan.ModelDirectory, "common.pb")).Should().BeFalse();
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_explicitFetchWithNotFound_rejectsWithoutRetrying()
    {
        var plan = CreateFixture();
        File.Delete(Path.Combine(plan.SourceDirectory, "common.pb"));
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var action = () => service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        var failure = await action.Should().ThrowAsync<HttpRequestException>();
        failure.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        handler.Requests.Should().Be(1);
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_cancelledDownload_preservesVerifiedCacheAndRemovesPartialWithoutRetrying()
    {
        var plan = CreateFixture();
        Directory.CreateDirectory(plan.ModelDirectory);
        var verifiedTarget = Path.Combine(plan.ModelDirectory, "common.pb");
        File.Copy(Path.Combine(plan.SourceDirectory, "common.pb"), verifiedTarget);
        File.Delete(Path.Combine(plan.SourceDirectory, "genre.json"));
        var handler = new FixtureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("fixture-genre")
        });
        using var client = new HttpClient(handler);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var service = new ModelPreparationService(client, allowDownloads: true, TimeSpan.Zero);

        var action = () => service.PrepareAsync(plan, progress =>
        {
            if (progress.Stage == "Downloading model" && progress.CompletedBytes > plan.Assets[0].Bytes)
            {
                cancellation.Cancel();
            }
        }, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().Be(1);
        File.ReadAllText(verifiedTarget).Should().Be("fixture-common");
        File.Exists(Path.Combine(plan.ModelDirectory, "genre.json")).Should().BeFalse();
        Directory.GetFiles(plan.ModelDirectory, "*.partial").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_symlinkCache_rejectsWithoutChangingLinkedFile()
    {
        if (OperatingSystem.IsWindows()) return;
        var plan = CreateFixture();
        Directory.CreateDirectory(plan.ModelDirectory);
        var outside = Path.Combine(_root, "outside");
        File.WriteAllText(outside, "outside-original");
        File.CreateSymbolicLink(Path.Combine(plan.ModelDirectory, "common.pb"), outside);

        var action = () => _service.PrepareAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<IOException>().WithMessage("*symbolic link*");
        File.ReadAllText(outside).Should().Be("outside-original");
    }

    [Fact]
    public void CreatePlan_sourceOverride_usesCheckoutPinnedManifest()
    {
        var plan = CreateFixture();
        var overrideDirectory = Path.Combine(_root, "existing-cached-models");

        var overridden = _service.CreatePlan(_source, _target, "effnet", overrideDirectory);

        overridden.SourceDirectory.Should().Be(overrideDirectory);
        overridden.Assets.Should().Equal(plan.Assets);
    }

    [Theory]
    [InlineData("effnet ../outside 14 fixture/outside")]
    [InlineData("effnet common.pb 14 ../common.pb")]
    [InlineData("effnet unknown.pb 14 fixture/unknown.pb")]
    public void CreatePlan_invalidOrUnpinnedRequirement_rejects(string malformed)
    {
        CreateFixture();
        File.WriteAllText(Path.Combine(_source, "classificators", "essentia", ModelPreparationService.RequirementsFile), malformed);

        var action = () => _service.CreatePlan(_source, _target, "effnet");

        action.Should().Throw<InvalidDataException>();
    }

    private ModelPreparationPlan CreateFixture()
    {
        var assetRoot = Path.Combine(_source, "classificators", "essentia");
        var sourceModels = Path.Combine(assetRoot, "models");
        var common = "fixture-common"u8.ToArray();
        var genre = "fixture-genre"u8.ToArray();
        File.WriteAllBytes(Path.Combine(sourceModels, "common.pb"), common);
        File.WriteAllBytes(Path.Combine(sourceModels, "genre.json"), genre);
        File.WriteAllText(Path.Combine(sourceModels, "models.sha256"),
            $"{Convert.ToHexString(SHA256.HashData(common))}  common.pb\n{Convert.ToHexString(SHA256.HashData(genre))}  genre.json\n");
        File.WriteAllText(Path.Combine(assetRoot, ModelPreparationService.RequirementsFile),
            $"shared common.pb {common.Length} fixture/common.pb\neffnet genre.json {genre.Length} fixture/genre.json\n");
        return _service.CreatePlan(_source, _target, "effnet");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FixtureHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = request.RequestUri;
            return Task.FromResult(respond(++Requests));
        }
    }
}
