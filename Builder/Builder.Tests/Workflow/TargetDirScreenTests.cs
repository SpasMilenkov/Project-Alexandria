using AwesomeAssertions;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class TargetDirScreenTests : IDisposable
{
    private readonly string _root;

    public TargetDirScreenTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "builder-target-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Backend"));
        Directory.CreateDirectory(Path.Combine(_root, "Frontend"));
    }

    [Fact]
    public void Run_detectedRepo_acceptsRepoAsTarget()
    {
        var keys = new FakeKeys().Enter()   // source-found info screen
            .Enter();                       // target: accept suggestion (= repo root)

        using var harness = new ShellHarness(keys);

        TargetDirScreen.RepoRootOverride = () => _root;

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.InstallPath.Should().Be(_root);
        harness.Context.SourceRoot.Should().Be(_root);

        // F9a: the two-location model is stated explicitly
        harness.Output.Should().Contain("builds from here");
    }

    [Fact]
    public void Run_repoNotDetected_asksForSourceThenTarget()
    {
        var source = Path.Combine(_root, "checkout");

        Directory.CreateDirectory(Path.Combine(source, "Backend"));
        Directory.CreateDirectory(Path.Combine(source, "Frontend"));

        var keys = new FakeKeys()
            .Text(source).Enter()   // source path prompt
            .Enter();               // target: accept suggested sibling folder

        using var harness = new ShellHarness(keys);

        TargetDirScreen.RepoRootOverride = () => null;

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Context.SourceRoot.Should().Be(source);
        harness.Context.InstallPath.Should().Contain("alexandria");
    }

    [Fact]
    public void Run_existingEnv_warnsButContinues()
    {
        File.WriteAllText(Path.Combine(_root, ".env"), "DB_PASSWORD=x");

        var keys = new FakeKeys().Enter()   // info
            .Enter()                        // target: accept repo root
            .Enter();                       // post-warning continue

        using var harness = new ShellHarness(keys);

        TargetDirScreen.RepoRootOverride = () => _root;

        var outcome = TargetDirScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Output.Should().Contain("already has an Alexandria configuration");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // temp cleanup is best effort
        }
    }
}
