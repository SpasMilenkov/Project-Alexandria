using AwesomeAssertions;
using Builder.AudioModels;
using Builder.Catalog;
using Builder.Common;
using Builder.Configuration;
using Builder.Credentials;
using Builder.Deployment;
using Builder.Docker;
using Builder.Network;
using Builder.Storage;
using Builder.SystemCheck;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Deployment;

public class InstallationOrchestratorTests : IDisposable
{
    private static readonly string[] Inventory =
    [
        "postgres", "rabbitmq-config", "rabbitmq", "garage", "garage-init", "api", "frontend", "nginx",
        "playlist-worker", "document-worker", "media-worker", "transpilation-worker", "media-metadata-worker", "lyrics-worker",
        "essentia-effnet-worker", "prometheus", "grafana", "cadvisor", "postgres-exporter", "alloy", "loki",
    ];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "builder-orchestration-tests", Guid.NewGuid().ToString("N"));
    private readonly InstallationContext _context;
    private readonly IConfigurationService _configuration = Substitute.For<IConfigurationService>();
    private readonly IDockerService _docker = Substitute.For<IDockerService>();
    private readonly ISystemChecker _systemChecker = Substitute.For<ISystemChecker>();
    private readonly IHttpsEndpointVerifier _httpsVerifier = Substitute.For<IHttpsEndpointVerifier>();
    private readonly IInstallationModelPreparation _models = Substitute.For<IInstallationModelPreparation>();
    private readonly IAdminLoginVerifier _adminLogin = Substitute.For<IAdminLoginVerifier>();
    private readonly List<string> _events = [];

    public InstallationOrchestratorTests()
    {
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Path.Combine(_directory, "init-output"));
        File.WriteAllText(Path.Combine(_directory, "init-output", StorageCapacityReceipt.FileName), "10000000000\n");
        _context = new InstallationContext { InstallPath = _directory };
        _context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);
        _context.Config.ProjectName = "builder-synthetic-pipeline";
        _context.Config.PublicEndpoint = new PublicEndpoint { Host = "192.0.2.10", HttpsPort = 8443 };
        _systemChecker.IsPortAvailable(Arg.Any<int>()).Returns(true);
        _context.Config.GenerationId = "synthetic-generation";
        _context.Config.EnvironmentValues = EnvGenerator.BuildValues(new FeatureSelection(),
            new CredentialService().GenerateAllCredentials(new FeatureSelection()), new AdminAccountInput(), null);
        _context.Config.EnvironmentValues["ALEXANDRIA_GENERATION_ID"] = _context.Config.GenerationId;
        _docker.ValidateConfiguration(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            _events.Add("validate");

            return true;
        });
        _docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(Inventory);
        _docker.ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            _events.Add("build");

            return true;
        });
        _docker.ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(true);
        _docker.ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            _events.Add("api");

            return true;
        });
        _docker.WaitForMonitoring(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Action<string>?>()).Returns(true);
        _docker.ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(true);
        _docker.WaitForServices(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<TimeSpan>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Action<IReadOnlyList<ContainerStatus>>?>(), Arg.Any<Action<string>?>())
            .Returns(true);
    }

    [Fact]
    public void Execute_unsupportedHost_rejectsBeforeContextProbeOrConfigurationChanges()
    {
        _systemChecker.GetHostPlatformError().Returns("This installer supports glibc-based Linux x64 hosts only. Detected ARM64.");

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*glibc-based Linux x64*ARM64*");
        _systemChecker.DidNotReceive().GetDockerContextError();
        _systemChecker.DidNotReceive().GetDockerPlatformError();
        AssertNoDeploymentChanges();
    }

    [Fact]
    public void Execute_contextChangedToRemote_rejectsBeforeDaemonProbeOrConfigurationChanges()
    {
        _systemChecker.GetDockerContextError().Returns("Select a local Docker context before retrying.");

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*local Docker context*");
        _systemChecker.DidNotReceive().GetDockerPlatformError();
        AssertNoDeploymentChanges();
    }

    [Fact]
    public void Execute_unsupportedDaemon_rejectsBeforeConfigurationChanges()
    {
        _systemChecker.GetDockerPlatformError().Returns("This installer requires a local Linux amd64 Docker daemon. Detected linux arm64.");

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*local Linux amd64*arm64*");
        AssertNoDeploymentChanges();
    }

    private void AssertNoDeploymentChanges()
    {
        _configuration.DidNotReceive().BeginGeneration(Arg.Any<InstallationConfig>(), Arg.Any<string>());
        _configuration.DidNotReceive().WriteAllConfigFiles(Arg.Any<InstallationConfig>(), Arg.Any<string>());
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
        File.Exists(Path.Combine(_directory, ".env")).Should().BeFalse();
        _context.DeploymentStarted.Should().BeFalse();
    }

    [Fact]
    public void Execute_freshInstallation_validatesBeforeBuildAndPersistsKeysBeforeStartingApps()
    {
        WriteHandoff();
        _docker.ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            GarageCredentialPersistence.HasPersistedKeys(_context).Should().BeTrue();
            File.Exists(HandoffPath).Should().BeFalse();
            File.ReadAllText(Path.Combine(_directory, "init-output", GarageCredentialPersistence.ReceiptFileName))
                .Should().Be("synthetic-generation\n");
            _events.Add("api");

            return true;
        });

        Execute();

        _events.IndexOf("validate").Should().BeLessThan(_events.IndexOf("build"));
        _events.IndexOf("Persisting storage keys").Should().BeLessThan(_events.IndexOf("api"));
        _docker.Received(1).WaitForServices(_directory,
            Arg.Is<IReadOnlyCollection<string>>(services => services.Count == Inventory.Length && Inventory.All(services.Contains)), Arg.Any<TimeSpan>(),
            Arg.Is<IReadOnlyCollection<string>>(services => services.Contains("garage-init") && services.Contains("rabbitmq-config")),
            Arg.Any<Action<IReadOnlyList<ContainerStatus>>?>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void StageCount_matchesTheStagesAFullRunReports(bool audio, bool monitoring, bool gpu)
    {
        WriteHandoff();
        _context.Config.Features.SetEnabled(FeatureCatalog.Monitoring.Id, monitoring);
        _context.Config.Features.SetEnabled(FeatureCatalog.Autotagging.Id, audio);
        var capabilities = ServiceCatalog.Resolve(_context.Config.Features);
        var stages = new List<string>();

        if (gpu)
        {
            WireGpus(Amd);
            ProbeReports(Passed(Amd));
        }

        _docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(capabilities.ServiceIds.ToArray());

        new InstallationOrchestrator(_configuration, _docker, _systemChecker, _httpsVerifier, _models, _adminLogin).Execute(_context, (label, action) =>
        {
            stages.Add(label);
            action(_ => { });
        });

        stages.Should().HaveCount(InstallationOrchestrator.StageCount(capabilities, _context.Config.EnvironmentValues));
    }

    [Fact]
    public void Execute_invalidCompose_stopsBeforeBuildOrInfrastructure()
    {
        _docker.ValidateConfiguration(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(false);

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*Compose rejected*");
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Execute_incompleteServiceInventory_stopsBeforeBuild()
    {
        _docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(Inventory.Where(service => service != "nginx").ToArray());

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*nginx*");
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Execute_missingSelectedWorker_stopsBeforeBuild()
    {
        _docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>())
            .Returns(Inventory.Where(service => service != "document-worker").ToArray());

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*document-worker*");
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Execute_serviceOutsideReviewedSelection_stopsBeforeBuild()
    {
        _docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>())
            .Returns(Inventory.Append("unreviewed-worker").ToArray());

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*outside the reviewed selection*unreviewed-worker*");
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Execute_initJobNotSuccessful_doesNotMergeKeysOrStartApps()
    {
        WriteHandoff();
        _docker.WaitForServices(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<TimeSpan>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Action<IReadOnlyList<ContainerStatus>>?>(), Arg.Any<Action<string>?>())
            .Returns(false);

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*initialization*");
        File.Exists(HandoffPath).Should().BeTrue();
        _context.Config.EnvironmentValues["GARAGE_S3_ACCESS_KEY"].Should().Be("placeholder");
        _docker.DidNotReceive().ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_missingOrMismatchingHandoff_stopsBeforeApps(bool mismatching)
    {
        if (mismatching)
        {
            WriteHandoff("another-generation");
        }

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>();
        _docker.DidNotReceive().ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
        File.Exists(HandoffPath).Should().Be(mismatching);
    }

    [Fact]
    public void Execute_existingCompleteKeys_resumesWithoutRequiringDeletedHandoff()
    {
        foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(new("GK1", "s1", "GK2", "s2", "GK3", "s3")))
        {
            _context.Config.EnvironmentValues[key] = value;
        }

        Execute();

        _docker.Received(1).ComposeUpApi(_directory, Arg.Any<Action<string>?>());
        _docker.Received(1).ComposeUpAll(_directory, Arg.Any<Action<string>?>());
        EnvGenerator.Parse(File.ReadAllText(Path.Combine(_directory, ".env")))["GARAGE_S3_ACCESS_KEY"].Should().Be("GK1");
        File.ReadAllText(Path.Combine(_directory, "init-output", GarageCredentialPersistence.ReceiptFileName))
            .Should().Be("synthetic-generation\n");
    }

    [Fact]
    public void Execute_olderInitImageExportsAgainDuringFullStartup_consumesDuplicateBeforeCompletion()
    {
        WriteHandoff();
        _docker.ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            WriteHandoff();
            return true;
        });

        Execute();

        File.Exists(HandoffPath).Should().BeFalse();
        File.ReadAllText(Path.Combine(_directory, "init-output", GarageCredentialPersistence.ReceiptFileName))
            .Should().Be("synthetic-generation\n");
        _context.DeploymentVerified.Should().BeTrue();
    }

    [Fact]
    public void Execute_finalSelectedServiceMissing_doesNotReportSuccess()
    {
        WriteHandoff();
        _docker.WaitForServices(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<TimeSpan>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Action<IReadOnlyList<ContainerStatus>>?>(), Arg.Any<Action<string>?>())
            .Returns(call => call.ArgAt<IReadOnlyCollection<string>>(1).Count < Inventory.Length);

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*selected services*");
    }

    [Fact]
    public void Execute_cancellationDuringInfrastructure_propagatesWithoutTearingDownStack()
    {
        _docker.WaitForServices(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<TimeSpan>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Action<IReadOnlyList<ContainerStatus>>?>(), Arg.Any<Action<string>?>())
            .Returns(_ =>
            {
                _context.Cancellation.Cancel();
                throw new OperationCanceledException(_context.Cancellation.Token);
            });

        var action = () => Execute();

        action.Should().Throw<OperationCanceledException>();
        _docker.DidNotReceive().ComposeUpApi(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Fact]
    public void Execute_environmentChangedAfterReview_preservesNewerFileAndStopsBeforeGeneration()
    {
        var environment = Path.Combine(_directory, ".env");
        _context.ApprovedEnvironmentContent = "CUSTOM_SETTING='reviewed'\n";
        const string current = "CUSTOM_SETTING='changed-after-review'\n";
        File.WriteAllText(environment, current);

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*changed after review*");
        File.ReadAllText(environment).Should().Be(current);
        _configuration.DidNotReceive().BeginGeneration(Arg.Any<InstallationConfig>(), Arg.Any<string>());
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_portOccupiedAfterReview_startsOnlyIfOwnedByThisInstallation(bool owned)
    {
        WriteHandoff();
        _systemChecker.IsPortAvailable(8443).Returns(false);
        _docker.IsHttpsPortOwned(_directory, 8443).Returns(owned);

        var action = () => Execute();

        if (owned)
        {
            action.Should().NotThrow();
            _docker.Received(1).ComposeUpAll(_directory, Arg.Any<Action<string>?>());
        }
        else
        {
            action.Should().Throw<InvalidOperationException>().WithMessage("*became occupied*preserved*");
            _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
        }

        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_redirectPortOccupiedAfterReview_startsOnlyIfOwnedByThisInstallation(bool owned)
    {
        WriteHandoff();
        _context.Config.Ports[PublicEndpoint.HttpRedirectPortKey] = PublicEndpoint.HttpRedirectPort;
        _systemChecker.IsPortAvailable(PublicEndpoint.HttpRedirectPort).Returns(false);
        _docker.IsHttpRedirectPortOwned(_directory, PublicEndpoint.HttpRedirectPort).Returns(owned);

        var action = () => Execute();

        if (owned)
        {
            action.Should().NotThrow();
            _docker.Received(1).ComposeUpAll(_directory, Arg.Any<Action<string>?>());
        }
        else
        {
            action.Should().Throw<InvalidOperationException>().WithMessage("*redirect became occupied*preserved*");
            _docker.DidNotReceive().ComposeUpAll(Arg.Any<string>(), Arg.Any<Action<string>?>());
        }

        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Fact]
    public void Execute_containersReadyButPublicHttpsFails_doesNotReportSuccessOrTearDown()
    {
        WriteHandoff();
        _httpsVerifier.When(verifier => verifier.Verify(Arg.Any<InstallationConfig>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("HTTPS address could not be verified"));

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*HTTPS address could not be verified*");
        _context.Completed.Should().BeFalse();
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Fact]
    public void Execute_modelPreparationFailure_stopsBeforeBuildingOrStartingServices()
    {
        _models.When(service => service.Prepare(Arg.Any<InstallationConfig>(), Arg.Any<Action<string>>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidDataException("Selected model checksum mismatch"));

        var action = () => Execute();

        action.Should().Throw<InvalidDataException>().WithMessage("*checksum mismatch*");
        _docker.DidNotReceive().ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>());
        _docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
    }

    [Fact]
    public void Execute_monitoringNotReady_stopsBeforeSuccessWithoutTearingDown()
    {
        WriteHandoff();
        _docker.WaitForMonitoring(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<Action<string>?>()).Returns(false);

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*Monitoring endpoints*");
        _httpsVerifier.DidNotReceive().Verify(Arg.Any<InstallationConfig>(), Arg.Any<CancellationToken>());
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    private string HandoffPath => Path.Combine(_directory, "init-output", "garage-credentials.env");

    [Fact]
    public void Execute_freshAdminVerification_runsAfterHttpsAndBeforeCompletion()
    {
        WriteHandoff();
        _adminLogin.When(service => service.Verify(Arg.Any<InstallationConfig>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                _events.Should().Contain("Checking the HTTPS address");
                _context.DeploymentVerified.Should().BeFalse();
                _context.Config.Stage.Should().NotBe("complete");
            });

        Execute();

        _adminLogin.Received(1).Verify(_context.Config, _context.Cancellation.Token);
        _context.AdminLoginVerified.Should().BeTrue();
        _context.DeploymentVerified.Should().BeTrue();
        _context.Config.Stage.Should().Be("complete");
        _events.Last().Should().Be("Saving the completed installation");
        File.ReadAllText(Path.Combine(_directory, InstallationSummary.FileName)).Should().Contain("Fresh administrator login");
    }

    [Fact]
    public void Execute_adminVerificationFails_doesNotCompleteOrResetAccount()
    {
        WriteHandoff();
        _adminLogin.When(service => service.Verify(Arg.Any<InstallationConfig>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Fresh administrator login rejected"));

        var action = () => Execute();

        action.Should().Throw<InvalidOperationException>().WithMessage("*login rejected*");
        _context.DeploymentVerified.Should().BeFalse();
        _context.AdminLoginVerified.Should().BeFalse();
        _context.Config.Stage.Should().NotBe("complete");
        _events.Should().NotContain("Saving the completed installation");
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    [Fact]
    public void Execute_repair_preservesAccountWithoutRequiringOriginalPassword()
    {
        WriteHandoff();
        _context.IsRepair = true;
        _context.Config.EnvironmentValues["ADMIN_PASSWORD"] = "obsolete-bootstrap-password";

        Execute();

        _adminLogin.DidNotReceive().Verify(Arg.Any<InstallationConfig>(), Arg.Any<CancellationToken>());
        _context.Config.EnvironmentValues["ADMIN_PASSWORD"].Should().Be("obsolete-bootstrap-password");
        _context.AdminLoginVerified.Should().BeFalse();
        _context.DeploymentVerified.Should().BeTrue();
        var report = File.ReadAllText(Path.Combine(_directory, InstallationSummary.FileName));
        report.Should().Contain("Current password and browser sign-in were not verified");
        report.Should().NotContain("obsolete-bootstrap-password");
        _docker.DidNotReceive().ComposeDown(Arg.Any<string>());
    }

    private static readonly RenderNode Amd = new("/dev/dri/by-path/pci-0000:04:00.0-render", "0000:04:00.0", "AMD", "226:128", 110);
    private static readonly RenderNode Intel = new("/dev/dri/by-path/pci-0000:00:02.0-render", "0000:00:02.0", "Intel", "226:129", 105);
    private readonly List<string> _probedWhileWiredTo = [];

    private void WireGpus(params RenderNode[] gpus)
    {
        _context.Resources.VideoGpus = gpus;
        var capabilities = ServiceCatalog.Resolve(_context.Features);
        ResourceBudget.ApplyEnvironment(_context.Config.EnvironmentValues, capabilities, _context.Resources);
        VideoEncodingEnvironment.Apply(_context.Config.EnvironmentValues, capabilities, _context.Resources);
    }

    private static string Passed(RenderNode gpu) =>
        $$"""{"device":"{{gpu.Path}}","usable":true,"driver":"Mesa driver for {{gpu.Vendor}}","detail":null}""";

    private static string Failed(RenderNode gpu) =>
        $$"""{"device":"{{gpu.Path}}","usable":false,"driver":null,"detail":"ffmpeg exited with code 1."}""";

    // Answers for the devices asked about, and records which device the written .env pointed the
    // worker at during each run, since a GPU must be tried with only its own access.
    private void ProbeReports(params string[] entries) =>
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(call =>
            {
                _events.Add("probe");
                var devices = call.ArgAt<IReadOnlyList<string>>(1);
                var written = EnvGenerator.Parse(File.ReadAllText(Path.Combine(_directory, ".env")));
                _probedWhileWiredTo.Add(string.Join(",", devices) + " wired to " + written[VideoEncodingEnvironment.DeviceNumberKey]);
                var answers = entries.Where(entry => devices.Any(device => entry.Contains($"\"{device}\""))).ToArray();

                return new ProcessResult(answers.Any(entry => entry.Contains("\"usable\":true")) ? 0 : 1,
                    "[" + string.Join(",", answers) + "]", string.Empty);
            });

    [Fact]
    public void Execute_gpuPassesItsTest_keepsItBetweenBuildAndInfrastructure()
    {
        WriteHandoff();
        WireGpus(Amd);
        ProbeReports(Passed(Amd));
        var limitsBefore = Limits();

        Execute();

        _events.IndexOf("build").Should().BeLessThan(_events.IndexOf("probe"));
        _events.IndexOf("probe").Should().BeLessThan(_events.IndexOf("Starting storage and messaging"));
        _probedWhileWiredTo.Should().Equal($"{Amd.Path} wired to 226:128");
        _configuration.Received(1).BeginGeneration(Arg.Any<InstallationConfig>(), Arg.Any<string>());
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.DeviceKey].Should().Be(Amd.Path);
        Limits().Should().Equal(limitsBefore);
        _context.VideoEncodingOutcome.Should().Be("On the GPU: Mesa driver for AMD");
    }

    [Fact]
    public void Execute_gpuFailsItsTest_rewiresTheWorkerToTheCpuBeforeAnythingStarts()
    {
        WriteHandoff();
        WireGpus(Amd);
        ProbeReports(Failed(Amd));
        _configuration.When(service => service.WriteAllConfigFiles(Arg.Any<InstallationConfig>(), Arg.Any<string>()))
            .Do(_ => _events.Add("write"));

        Execute();

        _events.LastIndexOf("write").Should().BeGreaterThan(_events.IndexOf("probe"));
        _events.LastIndexOf("validate").Should().BeLessThan(_events.IndexOf("Starting storage and messaging"));
        _configuration.Received(2).BeginGeneration(Arg.Any<InstallationConfig>(), _directory);
        var written = EnvGenerator.Parse(File.ReadAllText(Path.Combine(_directory, ".env")));
        written[VideoEncodingEnvironment.ModeKey].Should().Be("Cpu");
        written.Should().NotContainKeys(VideoEncodingEnvironment.DeviceKey, VideoEncodingEnvironment.GroupKey,
            VideoEncodingEnvironment.DeviceNumberKey);
        _context.Config.EnvironmentValues.Should().NotContainKey(VideoEncodingEnvironment.DeviceKey);
        _context.Resources.VideoGpus.Should().BeEmpty();
        _context.Resources.VideoGpuUnavailableReason.Should().Be("no detected GPU could encode in the test after the build");
        _context.VideoEncodingOutcome.Should().Be("On the CPU, because no GPU could encode");
    }

    // The worker may only open the GPU it is wired to, so each GPU is tried on its own after the
    // files have been rewritten for it.
    [Fact]
    public void Execute_firstGpuFailsAndSecondPasses_triesEachWiredOnItsOwnAndKeepsTheSecond()
    {
        WriteHandoff();
        WireGpus(Amd, Intel);
        ProbeReports(Failed(Amd), Passed(Intel));

        Execute();

        _probedWhileWiredTo.Should().Equal($"{Amd.Path} wired to 226:128", $"{Intel.Path} wired to 226:129");
        _configuration.Received(2).BeginGeneration(Arg.Any<InstallationConfig>(), _directory);
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.DeviceKey].Should().Be(Intel.Path);
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.GroupKey].Should().Be("105");
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.DeviceNumberKey].Should().Be("226:129");
        _context.VideoEncodingOutcome.Should().Be("On the GPU: Mesa driver for Intel");
    }

    // A retry after a rewire to a later GPU failed starts with the files wired to that GPU, so the
    // first candidate is wired back before it is tried.
    [Fact]
    public void Execute_settingsWiredToALaterCandidate_rewiresTheFirstBeforeTryingIt()
    {
        WriteHandoff();
        WireGpus(Intel);
        _context.Resources.VideoGpus = [Amd, Intel];
        ProbeReports(Passed(Amd));

        Execute();

        _probedWhileWiredTo.Should().Equal($"{Amd.Path} wired to 226:128");
        _configuration.Received(2).BeginGeneration(Arg.Any<InstallationConfig>(), _directory);
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.DeviceKey].Should().Be(Amd.Path);
        _context.VideoEncodingOutcome.Should().Be("On the GPU: Mesa driver for AMD");
    }

    [Fact]
    public void Execute_everyGpuFails_endsOnTheCpu()
    {
        WriteHandoff();
        WireGpus(Amd, Intel);
        ProbeReports(Failed(Amd), Failed(Intel));

        Execute();

        _probedWhileWiredTo.Should().HaveCount(2);
        _configuration.Received(3).BeginGeneration(Arg.Any<InstallationConfig>(), _directory);
        _context.Config.EnvironmentValues[VideoEncodingEnvironment.ModeKey].Should().Be("Cpu");
        _context.VideoEncodingOutcome.Should().Be("On the CPU, because no GPU could encode");
    }

    [Fact]
    public void Execute_gpuTestCannotRun_fallsBackToTheCpuWithTheReason()
    {
        WriteHandoff();
        WireGpus(Amd);
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(new ProcessResult(1, string.Empty, "Error response from daemon: no such image\n"));

        Execute();

        _context.Config.EnvironmentValues[VideoEncodingEnvironment.ModeKey].Should().Be("Cpu");
        _context.VideoEncodingOutcome.Should().Be("On the CPU, because the test did not run: Error response from daemon: no such image");
    }

    // A test that never ran says nothing about the GPU, so going back to the review wires it
    // again instead of reporting that no GPU was found.
    [Fact]
    public void Execute_gpuTestCannotRun_keepsTheGpuForALaterAttempt()
    {
        WriteHandoff();
        WireGpus(Amd);
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(new ProcessResult(1, string.Empty, "Error response from daemon: no such image\n"));

        Execute();

        _context.Resources.VideoGpus.Should().Equal(Amd);
        _context.Resources.VideoGpuUnavailableReason.Should().BeNull();
        VideoEncodingEnvironment.Plan(_context.Resources).Should().StartWith("On the AMD GPU");
    }

    // Compose prints its own progress on stderr, which says nothing about why the probe failed.
    [Fact]
    public void Execute_gpuTestEndsInComposeProgress_reportsTheExitCodeInstead()
    {
        WriteHandoff();
        WireGpus(Amd);
        _docker.ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>())
            .Returns(new ProcessResult(139, string.Empty,
                " Container alexandria-transpilation-worker-run-1a2b Creating\n Container alexandria-transpilation-worker-run-1a2b Created\n"));

        Execute();

        _context.VideoEncodingOutcome.Should().Be("On the CPU, because the test did not run: the worker exited with code 139 without a report");
    }

    [Fact]
    public void Execute_noGpuWired_runsNoGpuTest()
    {
        WriteHandoff();
        WireGpus();

        Execute();

        _events.Should().NotContain(VideoGpuTest.StageLabel);
        _docker.DidNotReceive().ProbeVideoGpus(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>?>());
    }

    private Dictionary<string, string> Limits() => _context.Config.EnvironmentValues
        .Where(pair => pair.Key.StartsWith(ResourceBudget.EnvironmentPrefix, StringComparison.Ordinal))
        .ToDictionary();

    private void WriteHandoff(string generation = "synthetic-generation")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HandoffPath)!);
        EnvGenerator.WriteFile(HandoffPath, new Dictionary<string, string>
        {
            ["ALEXANDRIA_GENERATION_ID"] = generation,
            ["GARAGE_S3_ACCESS_KEY"] = "GK1", ["GARAGE_S3_SECRET_KEY"] = "s1",
            ["GARAGE_PREVIEW_KEY_ID"] = "GK2", ["GARAGE_PREVIEW_SECRET"] = "s2",
            ["GARAGE_STREAMING_KEY_ID"] = "GK3", ["GARAGE_STREAMING_SECRET"] = "s3",
        });
    }

    private void Execute()
    {
        new InstallationOrchestrator(_configuration, _docker, _systemChecker, _httpsVerifier, _models, _adminLogin).Execute(_context, (label, action) =>
        {
            _events.Add(label);
            action(_ => { });
        });
    }

    public void Dispose()
    {
        _context.Cancellation.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
