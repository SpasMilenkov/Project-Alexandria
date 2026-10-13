using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.Tests.Services;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Workflow;

// Minimal coverage for F9b failure truth-telling: the streamed output must
// reach the failure panel even when no containers exist yet.
[Collection(nameof(StaticConsoleTests))]
public class DeployFailureTests : IDisposable
{
    private readonly string _installPath;

    public DeployFailureTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), "builder-deploy-fail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_installPath);
    }

    [Fact]
    public void Run_buildFailure_streamsErrorIntoFailurePanel()
    {
        var context = new InstallationContext
        {
            Features = FeatureSelection.FromDefaults(),
            InstallPath = _installPath,
            SourceRoot = _installPath,
            AdminAccount = new AdminAccountInput
            {
                Email = "o@e.com",
                Username = "o",
                Password = "a-long-enough-password",
            },
        };

        context.Features.SetEnabled(FeatureCatalog.Autotagging.Id, false);

        var docker = Substitute.For<IDockerService>();

        docker.ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>())
            .Returns(ci =>
            {
                ci.ArgAt<Action<string>?>(1)?.Invoke("#14 ERROR: failed to solve: network timeout");

                return false;
            });

        docker.GetContainerStatuses(Arg.Any<string>()).Returns([]);
        docker.ValidateConfiguration(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(true);
        docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(
            ["postgres", "rabbitmq-config", "rabbitmq", "garage", "garage-init", "api", "frontend", "nginx",
                "playlist-worker", "document-worker", "media-worker", "transpilation-worker", "lyrics-worker"]);

        var configService = Substitute.For<IConfigurationService>();

        // failure menu: Show status, then Enter on the closing note
        using var harness = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Enter().Enter().Press(ConsoleKey.Escape)));

        harness.Context.Features = context.Features;
        harness.Context.InstallPath = context.InstallPath;
        harness.Context.SourceRoot = context.SourceRoot;
        harness.Context.AdminAccount = context.AdminAccount;
        PrepareEffectiveConfiguration(harness.Context);

        var outcome = DeployScreen.Run(harness.Shell, configService, docker, Substitute.For<ISystemChecker>());

        outcome.Should().Be(ScreenOutcome.Abort);
        harness.Output.Should().Contain("Installation stopped");
        harness.Output.Should().Contain("failed to solve");
        File.ReadAllText(harness.Context.DeploymentLogPath!).Should().Contain("image build failed");
    }

    [Fact]
    public void Run_stepThrows_streamsExceptionIntoFailurePanel()
    {
        var context = new InstallationContext
        {
            Features = FeatureSelection.FromDefaults(),
            InstallPath = _installPath,
        };

        var docker = Substitute.For<IDockerService>();

        docker.GetContainerStatuses(Arg.Any<string>()).Returns([]);

        // WriteAllConfigFiles throwing simulates any config-layer explosion;
        // D54 demands it surfaces in the panel instead of vanishing
        var configService = Substitute.For<IConfigurationService>();

        configService
            .When(c => c.WriteAllConfigFiles(Arg.Any<InstallationConfig>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("boom from config writer"));

        // failure menu: Esc = Exit
        using var harness = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Press(ConsoleKey.Escape)));

        harness.Context.Features = context.Features;
        harness.Context.InstallPath = context.InstallPath;
        PrepareEffectiveConfiguration(harness.Context);

        var outcome = DeployScreen.Run(harness.Shell, configService, docker, Substitute.For<ISystemChecker>());

        outcome.Should().Be(ScreenOutcome.Abort);
        harness.Output.Should().Contain("Installation stopped");
        harness.Output.Should().Contain("boom from config writer");
        File.ReadAllText(harness.Context.DeploymentLogPath!).Should().Contain("[error] boom from config writer");
    }

    private static void PrepareEffectiveConfiguration(InstallationContext context)
    {
        context.Config.ProjectName = "builder-synthetic-failure";
        context.Config.GenerationId = "synthetic-generation";
        context.Config.EnvironmentValues = EnvGenerator.BuildValues(context.Features,
            new CredentialService().GenerateAllCredentials(context.Features), context.AdminAccount, null);
    }

    [Fact]
    public void Run_retryBuild_retainsConfigurationAndRevalidatesCompose()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        var docker = FailingBuild(config);
        using var harness = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Down().Enter().Press(ConsoleKey.Escape)));
        harness.Context.Config = config;

        var outcome = DeployScreen.Run(harness.Shell, fixture.Service, docker, Substitute.For<ISystemChecker>());

        outcome.Should().Be(ScreenOutcome.Abort);
        docker.Received(2).ComposeBuild(config.InstallPath, Arg.Any<Action<string>?>());
        docker.Received(2).ValidateConfiguration(config.InstallPath, Arg.Any<Action<string>?>());
        docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
        docker.DidNotReceive().ComposeDown(Arg.Any<string>());
        File.ReadAllText(harness.Context.DeploymentLogPath!).Should().Contain("[resume] Retaining completed preparation: Writing configuration files");
        harness.Output.Should().NotContain("Synthetic1!");
        File.ReadAllText(harness.Context.DeploymentLogPath!).Should().NotContain("Synthetic1!");
    }

    [Fact]
    public void Run_generatedComposeChangedBeforeRetry_returnsToReviewWithoutExecutingChangedDeployment()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        var docker = FailingBuild(config);
        docker.ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(_ =>
        {
            File.AppendAllText(Path.Combine(config.InstallPath, "docker-compose.yml"), "\n# edited during deployment\n");
            return false;
        });
        using var harness = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Down().Enter().Enter()));
        harness.Context.Config = config;

        var outcome = DeployScreen.Run(harness.Shell, fixture.Service, docker, Substitute.For<ISystemChecker>());

        outcome.Should().Be(ScreenOutcome.Back);
        harness.Context.ShouldAbort.Should().BeFalse();
        docker.Received(1).ComposeBuild(config.InstallPath, Arg.Any<Action<string>?>());
        docker.DidNotReceive().ComposeUpCore(Arg.Any<string>(), Arg.Any<Action<string>?>());
        harness.Output.Should().Contain("Generated files").And.Contain("changed");
    }

    [Fact]
    public void Run_failureReturnToSummary_preservesServicesAndDoesNotMarkComplete()
    {
        using var fixture = new ConfigurationArtifactFixture();
        var config = fixture.Resolve();
        var docker = FailingBuild(config);
        using var harness = new ShellHarness(new WaitOnlyKeys(new FakeKeys().Down().Down().Down().Down().Enter()));
        harness.Context.Config = config;

        DeployScreen.Run(harness.Shell, fixture.Service, docker, Substitute.For<ISystemChecker>()).Should().Be(ScreenOutcome.Back);

        harness.Context.ShouldAbort.Should().BeFalse();
        harness.Context.DeploymentVerified.Should().BeFalse();
        docker.DidNotReceive().ComposeDown(Arg.Any<string>());
        File.Exists(harness.Context.DeploymentLogPath).Should().BeTrue();
    }

    private static IDockerService FailingBuild(InstallationConfig config)
    {
        var docker = Substitute.For<IDockerService>();
        docker.ValidateConfiguration(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(true);
        docker.GetExpectedServices(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(ServiceCatalog.Resolve(config.Features).ServiceIds);
        docker.ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>()).Returns(call =>
        {
            call.ArgAt<Action<string>?>(1)?.Invoke("build error with ADMIN_PASSWORD=Synthetic1!");
            return false;
        });

        return docker;
    }

    private sealed class WaitOnlyKeys(FakeKeys keys) : IKeySource
    {
        public ConsoleKeyInfo Wait() => keys.Wait();

        public ConsoleKeyInfo? Poll() => null;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_installPath, recursive: true);
        }
        catch
        {
            // temp cleanup is best effort
        }
    }
}
