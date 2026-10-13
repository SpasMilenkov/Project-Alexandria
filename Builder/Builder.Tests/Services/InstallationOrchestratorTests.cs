using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Services;

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
