using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class SummaryAndSuccessScreenTests
{
    [Theory]
    [InlineData(false, "downloads disabled")]
    [InlineData(true, "essentia.upf.edu (HTTPS)")]
    public void Summary_analysisSelected_showsReviewedDownloadPermission(bool allowDownloads, string expected)
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.Features = new Builder.Models.FeatureSelection();
        harness.Context.Features.SetAudioBackbone("effnet");
        harness.Context.Features.SetEnabled(Builder.Models.FeatureCatalog.Autotagging.Id, true);
        harness.Context.Config.AllowModelDownloads = allowDownloads;

        SummaryScreen.Run(harness.Shell, new ResourceCalculator());

        harness.Output.Should().Contain(expected);
    }

    [Fact]
    public void Summary_coreOnly_separatesOngoingServicesFromSetupJobsAndOmitsModels()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.Features = new Builder.Models.FeatureSelection();

        SummaryScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Output.Should().Contain("8 ongoing").And.Contain("2 setup jobs");
        harness.Output.Should().Contain("4 GB");
        harness.Output.Should().NotContain("Audio analysis models");
    }

    [Fact]
    public void Summary_analysisSelected_displaysModelRequirementsAndCoordinatorMemory()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());
        harness.Context.Features = new Builder.Models.FeatureSelection { Enabled = ["audio-tagging-fast", "autotagging"] };

        SummaryScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Output.Should().Contain("Audio analysis models");
        harness.Output.Should().Contain("Audio analysis support");
        harness.Output.Should().Contain("7 GB");
    }

    [Fact]
    public void Summary_serviceView_includesExplicitMemoryHeadroom()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("a").Enter());
        harness.Context.Features = new Builder.Models.FeatureSelection();

        SummaryScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Output.Should().Contain("Memory headroom").And.Contain("576 MB");
        harness.Output.Should().Contain("once, during setup");
    }

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

        var outcome = SummaryScreen.Run(h.Shell, new ResourceCalculator());

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

        var outcome = SummaryScreen.Run(h.Shell, new ResourceCalculator());

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

        var outcome = SummaryScreen.Run(h.Shell, new ResourceCalculator());

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

        var outcome = SummaryScreen.Run(h.Shell, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Back);
        h.Context.ShouldAbort.Should().BeFalse("Esc means re-edit, not abort");
    }

    [Fact]
    public void Summary_backAndForward_doesNotGenerateOrReplaceCredentials()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape).Enter());
        var original = harness.Context.Config.Credentials;
        original["DB_PASSWORD"] = "preserve-this-password";

        SummaryScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Back);
        SummaryScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Context.Config.Credentials.Should().BeSameAs(original);
        original["DB_PASSWORD"].Should().Be("preserve-this-password");
        harness.Output.Should().NotContain("preserve-this-password");
    }

    [Fact]
    public void Success_showsDashboard_withoutLeakingSecrets()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.D).Press(ConsoleKey.End).Enter().Enter());
        harness.Context.DeploymentVerified = true;

        harness.Context.Features = Builder.Models.FeatureSelection.FromDefaults();
        harness.Context.Features.SetEnabled(Builder.Models.FeatureCatalog.Monitoring.Id, true);
        harness.Context.Config.PublicEndpoint.HttpsPort = 8443;
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
        harness.Output.Should().Contain("https://192.0.2.10:8443");
        harness.Output.Should().Contain("/monitoring/");
        var details = string.Join("\n", InstallationSummary.Describe(harness.Context));
        details.Should().Contain("--project-name").And.Contain(".env");
        details.Should().Contain("--no-build --pull never").And.Contain("stop");
        details.Should().NotContain("a-long-enough-password");
        harness.Output.Should().NotContain("a-long-enough-password");
    }
}
