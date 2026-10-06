using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
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

        var docker = Substitute.For<IDockerService>();

        docker.ComposeBuild(Arg.Any<string>(), Arg.Any<Action<string>?>())
            .Returns(ci =>
            {
                ci.ArgAt<Action<string>?>(1)?.Invoke("#14 ERROR: failed to solve: network timeout");

                return false;
            });

        docker.GetContainerStatuses(Arg.Any<string>()).Returns([]);

        var configService = new ConfigurationService(new TemplateService("Local"));

        // failure menu: Show status, then Enter on the closing note
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter());

        harness.Context.Features = context.Features;
        harness.Context.InstallPath = context.InstallPath;
        harness.Context.SourceRoot = context.SourceRoot;
        harness.Context.AdminAccount = context.AdminAccount;

        var outcome = DeployScreen.Run(harness.Shell, configService, docker);

        outcome.Should().Be(ScreenOutcome.Abort);
        harness.Output.Should().Contain("Installation stopped");
        harness.Output.Should().Contain("failed to solve");
        harness.Output.Should().Contain("docker compose build"); // retry hint, not logs
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
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        harness.Context.Features = context.Features;
        harness.Context.InstallPath = context.InstallPath;

        var outcome = DeployScreen.Run(harness.Shell, configService, docker);

        outcome.Should().Be(ScreenOutcome.Abort);
        harness.Output.Should().Contain("Installation stopped");
        harness.Output.Should().Contain("[error] boom from config writer");
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
