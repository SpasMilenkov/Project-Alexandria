using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class DeploymentResponsivenessTests
{
    [Fact]
    public void RunResponsive_silentWorker_themeChangesBeforeWorkerCompletes()
    {
        using var changed = new ManualResetEventSlim();
        using var harness = new ShellHarness(new PollKeys(new FakeKeys().T()));
        harness.Shell.ThemeSaver = _ => { changed.Set(); return true; };

        var result = harness.Shell.RunResponsive(() => changed.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        result.Should().BeTrue();
        Theme.Active.Id.Should().NotBe(Themes.Default.Id);
    }

    [Fact]
    public void RunResponsive_keyCancellation_awaitsWorkerFinallyBeforeUnwinding()
    {
        using var entered = new ManualResetEventSlim();
        var drained = false;
        using var harness = new ShellHarness(new PollKeys(new FakeKeys().Press(ConsoleKey.C, ctrl: true), entered));

        var action = () => harness.Shell.RunResponsive(() =>
        {
            entered.Set();

            try
            {
                harness.Context.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                harness.Context.Cancellation.Token.ThrowIfCancellationRequested();
                return true;
            }
            finally
            {
                drained = true;
            }
        });

        action.Should().Throw<ShellAbortException>();
        drained.Should().BeTrue();
        harness.Context.ShouldAbort.Should().BeTrue();
        harness.Output.Should().NotContain("What now?");
    }

    [Fact]
    public void RunResponsive_externalCancellation_awaitsWorkerAndPropagatesCancellation()
    {
        using var harness = new ShellHarness(new PollKeys(new FakeKeys()));
        var drained = false;

        var action = () => harness.Shell.RunResponsive(() =>
        {
            try
            {
                harness.Context.Cancellation.Cancel();
                harness.Context.Cancellation.Token.ThrowIfCancellationRequested();
                return true;
            }
            finally
            {
                drained = true;
            }
        });

        action.Should().Throw<OperationCanceledException>();
        drained.Should().BeTrue();
    }

    [Fact]
    public void Inspect_smallTerminal_scrollsToLongEvidenceWithoutDroppingTail()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.End).Enter());
        harness.Console.Profile.Width = 60;
        harness.Console.Profile.Height = 14;

        DeployScreen.Inspect(harness.Shell, "Evidence", "/tmp/installer.log", [new string('x', 300) + " CRITICAL_FINAL_EVIDENCE"]);

        harness.Output.Should().Contain("CRITICAL_FINAL_EVIDENCE");
        harness.Output.Should().Contain("/tmp/installer.log").And.Contain("PgUp/PgDn");
    }

    [Fact]
    public void Success_unverifiedDeployment_abortsBeforeClaimingSuccess()
    {
        using var harness = new ShellHarness(new FakeKeys());

        SuccessScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Abort);
        harness.Output.Should().Contain("Required checks have not completed");
        harness.Output.Should().NotContain("up and running");
    }

    private sealed class PollKeys(FakeKeys keys, ManualResetEventSlim? entered = null) : IKeySource
    {
        public ConsoleKeyInfo Wait() => keys.Wait();

        public ConsoleKeyInfo? Poll()
        {
            if (entered is not null && !entered.IsSet) return null;

            return keys.Poll();
        }
    }
}
