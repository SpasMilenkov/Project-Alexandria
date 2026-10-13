using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Support;
using Builder.Workflow;
using NSubstitute;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class CancellationOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleanup_existingTargetOrRepair_neverStopsAnInstallation(bool deploymentStarted)
    {
        var docker = Substitute.For<IDockerService>();
        var flow = new MainFlow(docker);
        flow.Context.InstallPath = Path.GetTempPath();
        flow.Context.IsRepair = true;
        flow.Context.DeploymentStarted = deploymentStarted;

        flow.Cleanup();

        flow.Context.Cancellation.IsCancellationRequested.Should().BeTrue();
        docker.DidNotReceiveWithAnyArgs().ComposeDown(default!);
    }

    [Fact]
    public void Keys_cancelledOutsideReadKey_unwindsWithoutConsumingInput()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.Cancellation.Cancel();

        var wait = () => harness.Shell.Keys.Wait();

        wait.Should().Throw<OperationCanceledException>();
        harness.Keys.Wait(TestContext.Current.CancellationToken).Key.Should().Be(ConsoleKey.Enter);
    }
}
