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
            .Text("Long-enough-Password1!").Enter()
            .Text("Long-enough-Password1!").Enter();

        using var h = new ShellHarness(keys);

        var outcome = AccountScreen.Run(h.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.AdminAccount.Email.Should().Be("owner@example.com");
        h.Context.AdminAccount.Username.Should().Be("owner");
        h.Context.AdminAccount.Password.Should().Be("Long-enough-Password1!");
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
            .Text("Long-enough-Password1!").Enter()
            .Text("different-password-here").Enter()   // rejected, stays on repeat
            .Press(ConsoleKey.U, 'u', ctrl: true)
            .Text("Long-enough-Password1!").Enter();

        using var h = new ShellHarness(keys);

        var outcome = AccountScreen.Run(h.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.AdminAccount.Password.Should().Be("Long-enough-Password1!");
    }

    [Fact]
    public void Run_backAndForward_preservesCustomizedUsernameAndMaskedPassword()
    {
        const string password = "My-Password1!";
        var keys = new FakeKeys()
            .Text("owner@example.com").Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text("custom.name").Enter()
            .Text(password).Enter().Press(ConsoleKey.Escape)
            .Enter().Enter().Enter().Text(password).Enter();

        using var harness = new ShellHarness(keys);

        AccountScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);
        AccountScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.AdminAccount.Username.Should().Be("custom.name");
        harness.Context.AdminAccount.Password.Should().Be(password);
        harness.Output.Should().NotContain(password);
    }

    [Fact]
    public void Run_changedEmail_updatesOnlyUncustomizedSuggestion()
    {
        const string password = "My-Password1!";
        var keys = new FakeKeys()
            .Text("first@example.com").Enter().Enter().Press(ConsoleKey.Escape)
            .Press(ConsoleKey.U, 'u', ctrl: true).Text("second@example.com").Enter()
            .Enter().Text(password).Enter().Text(password).Enter();

        using var harness = new ShellHarness(keys);

        AccountScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Back);
        AccountScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.AdminAccount.Username.Should().Be("second");
    }

    [Fact]
    public void Run_invalidPassword_keepsEditingUntilBackendRulesPass()
    {
        const string password = "My-Password1!";
        var keys = new FakeKeys()
            .Text("owner@example.com").Enter().Enter()
            .Text("long-but-no-uppercase-or-number").Enter()
            .Press(ConsoleKey.U, 'u', ctrl: true).Text(password).Enter()
            .Text(password).Enter();

        using var harness = new ShellHarness(keys);

        AccountScreen.Run(harness.Shell).Should().Be(ScreenOutcome.Continue);

        harness.Context.AdminAccount.Password.Should().Be(password);
        harness.Output.Should().Contain("uppercase");
    }
}
