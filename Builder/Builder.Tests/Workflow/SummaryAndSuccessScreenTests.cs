using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class SummaryAndSuccessScreenTests
{
    [Fact]
    public void Summary_confirmed_continuesWithoutAborting()
    {
        using var h = new ShellHarness(new FakeKeys().Enter());

        h.Context.Features = Builder.Models.FeatureSelection.FromDefaults();

        h.Context.AdminAccount = new Builder.Models.AdminAccountInput
        {
            Email = "owner@example.com",
            Username = "owner",
            Password = "a-long-enough-password",
        };

        var outcome = SummaryScreen.Run(h.Shell, new CredentialService(), new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Context.ShouldAbort.Should().BeFalse();
        h.Output.Should().Contain("Memory this will use");
        h.Output.Should().Contain("docker-compose.yml");
    }

    [Fact]
    public void Summary_breakdownIsDefault_andRFlipsToBar()
    {
        var keys = new FakeKeys().Text("r").Enter();
        using var h = new ShellHarness(keys);

        h.Context.AdminAccount = new Builder.Models.AdminAccountInput
        {
            Email = "o@e.com",
            Username = "o",
            Password = "a-long-enough-password",
        };

        var outcome = SummaryScreen.Run(h.Shell, new CredentialService(), new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Output.Should().Contain("(breakdown)"); // first frame: default
        h.Output.Should().Contain("switch to breakdown"); // second frame hint after flip
        h.Output.Should().Contain("(bar)"); // flipped frame caption
    }

    [Fact]
    public void Summary_aTogglesServiceView_withDescriptions()
    {
        var keys = new FakeKeys().Text("a").Enter();
        using var h = new ShellHarness(keys);

        h.Context.Features = Builder.Models.FeatureSelection.FromDefaults();

        h.Context.AdminAccount = new Builder.Models.AdminAccountInput
        {
            Email = "o@e.com",
            Username = "o",
            Password = "a-long-enough-password",
        };

        var outcome = SummaryScreen.Run(h.Shell, new CredentialService(), new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Output.Should().Contain("What it does");
        h.Output.Should().Contain("File storage");
    }

    [Fact]
    public void Summary_escape_returnsBackForReediting()
    {
        using var h = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        h.Context.AdminAccount = new Builder.Models.AdminAccountInput
        {
            Email = "o@e.com",
            Username = "o",
            Password = "a-long-enough-password",
        };

        var outcome = SummaryScreen.Run(h.Shell, new CredentialService(), new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Back);
        h.Context.ShouldAbort.Should().BeFalse("Esc means re-edit, not abort");
    }

    [Fact]
    public void Success_showsDashboard_withoutLeakingSecrets()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());

        harness.Context.Features = Builder.Models.FeatureSelection.FromDefaults();
        harness.Context.Config.Ports["HTTP_PORT"] = 8080;
        harness.Context.InstallPath = "/tmp/alexandria-test";

        harness.Context.AdminAccount = new Builder.Models.AdminAccountInput
        {
            Email = "owner@example.com",
            Username = "owner",
            Password = "a-long-enough-password",
        };

        var outcome = SuccessScreen.Run(harness.Shell);

        outcome.Should().Be(ScreenOutcome.Continue);
        harness.Output.Should().Contain("Alexandria is up and running");
        harness.Output.Should().Contain("http://localhost:8080");
        harness.Output.Should().Contain(".env");
        harness.Output.Should().NotContain("a-long-enough-password");
    }
}
