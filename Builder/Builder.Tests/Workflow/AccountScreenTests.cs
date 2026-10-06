using AwesomeAssertions;
using Builder.Tests.Support;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class AccountScreenTests
{
    [Fact]
    public void Run_happyPath_populatesAdminAccount()
    {
        var keys = new FakeKeys()
            .Text("owner@example.com").Enter()   // email
            .Enter()                              // display name: accept default
            .Text("a-long-enough-password").Enter()
            .Text("a-long-enough-password").Enter();

        using var h = new ShellHarness(keys);

        var outcome = AccountScreen.Run(h.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.AdminAccount.Email.Should().Be("owner@example.com");
        h.Context.AdminAccount.Username.Should().Be("owner");
        h.Context.AdminAccount.Password.Should().Be("a-long-enough-password");
    }

    [Fact]
    public void Run_escapeOnFirstField_returnsBack()
    {
        using var h = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        var outcome = AccountScreen.Run(h.Shell);

        outcome.Should().Be(ScreenOutcome.Back);
    }

    [Fact]
    public void Run_mismatchedRepeat_rejectsUntilMatching()
    {
        var keys = new FakeKeys()
            .Text("owner@example.com").Enter()
            .Enter()
            .Text("a-long-enough-password").Enter()
            .Text("different-password-here").Enter()   // rejected, stays on repeat
            .Backspaces(23)
            .Text("a-long-enough-password").Enter();

        using var h = new ShellHarness(keys);

        var outcome = AccountScreen.Run(h.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.AdminAccount.Password.Should().Be("a-long-enough-password");
    }
}
