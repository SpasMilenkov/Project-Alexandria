using AwesomeAssertions;
using Builder.Catalog;
using Builder.SystemCheck;
using Builder.Tests.Support;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.UI.Screens;

[Collection(nameof(StaticConsoleTests))]
public class FeaturesScreenTests
{
    private static IEnumerable<string> EnabledIds(FeatureSelection features) =>
        FeatureCatalog.All.Where(f => features.IsEnabled(f.Id)).Select(f => f.Id);

    [Fact]
    public void Run_confirmImmediately_keepsCatalogDefaults()
    {
        using var h = new ShellHarness(new FakeKeys().Enter().Enter());

        var outcome = FeaturesScreen.Run(h.Shell, new ResourceCalculator());
        var expected = FeatureSelection.FromDefaults();

        outcome.Should().Be(ScreenOutcome.Continue);
        EnabledIds(h.Context.Features).Should().BeEquivalentTo(EnabledIds(expected), o => o.WithStrictOrdering());
    }

    [Fact]
    public void Run_escape_returnsBack()
    {
        using var h = new ShellHarness(new FakeKeys().Press(ConsoleKey.Escape));

        var outcome = FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Back);
    }

    [Fact]
    public void Run_selectMaest_replacesEffnet()
    {
        // The model choice follows the feature toggles. Effnet is the default.
        using var h = new ShellHarness(new FakeKeys().Enter().Down().Enter());

        FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        h.Context.Features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id).Should().BeTrue();
    }

    [Fact]
    public void Run_featureTurnedOff_previewsWhatItWouldAdd()
    {
        // Monitoring is the last toggle; Space turns it off while it is highlighted
        using var h = new ShellHarness(new FakeKeys().Down().Down().Down().Down().Down().Space().Enter().Enter());
        h.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);

        FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        h.Context.Features.IsEnabled(FeatureCatalog.Monitoring.Id).Should().BeFalse();
        h.Output.Should().Contain("Would turn on ·").And.Contain("+1.5 GB with Monitoring dashboard");
        h.Output.Should().Contain("Metrics collector").And.Contain("▒");
    }

    [Fact]
    public void Run_mediaPreviewsHighlighted_showsTheWorkerThatRunsEitherWay()
    {
        // Media previews is the first row and only changes what an
        // always-running worker does, so its memory is shown, not added
        using var h = new ShellHarness(new FakeKeys().Enter().Enter());

        FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        h.Output.Should().Contain("Runs whether this is on or off").And.Contain("highlighted part runs either way");
        h.Output.Should().Contain("█ Media metadata and previews 1 GB");
    }

    [Fact]
    public void Run_selectionLargerThanThisComputer_saysSoBesideTheBar()
    {
        using var h = new ShellHarness(new FakeKeys().Enter().Enter());

        FeaturesScreen.Run(h.Shell, new ResourceCalculator(), machineMemoryMb: 4096);

        h.Output.Should().Contain("more than this computer's 4 GB");
    }

    [Fact]
    public void Run_highlightingStreaming_showsItsComponents()
    {
        // Adaptive streaming sits two rows below the highlight start; the
        // detail pane re-renders on every move, so the component list for it
        // must appear before Enter confirms the defaults
        using var h = new ShellHarness(new FakeKeys().Down().Down().Enter().Enter());

        var outcome = FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Output.Should().Contain("Turns on ·");
        h.Output.Should().Contain("Transpilation worker");
    }

    [Fact]
    public void Run_enablingMonitoring_growsServiceCountBySix()
    {
        var calculator = new ResourceCalculator();
        var before = calculator.CountServices(FeatureSelection.FromDefaults());

        // Monitoring is the last of six feature toggles.
        using var h = new ShellHarness(new FakeKeys().Down().Down().Down().Down().Down().Space().Enter().Enter());

        FeaturesScreen.Run(h.Shell, calculator);

        var after = calculator.CountServices(h.Context.Features);

        (after - before).Should().Be(6); // prometheus, grafana, cadvisor, exporter, alloy, loki
    }

    [Fact]
    public void Run_backAndForward_retainsChangesInTheSameConfiguration()
    {
        using var harness = new ShellHarness(new FakeKeys().Space().Press(ConsoleKey.Escape).Enter().Enter());

        FeaturesScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Back);
        FeaturesScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Context.Features.IsEnabled(FeatureCatalog.MediaProcessing.Id).Should().BeFalse();
        harness.Context.Config.Features.Should().BeSameAs(harness.Context.Features);
    }
    [Fact]
    public void Run_escapeFromModelChoice_retainsTogglesBeforeReturningToModelChoice()
    {
        using var harness = new ShellHarness(new FakeKeys().Space().Enter()
            .Press(ConsoleKey.Escape).Enter().Down().Enter());

        FeaturesScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Context.Features.IsEnabled(FeatureCatalog.MediaProcessing.Id).Should().BeFalse();
        harness.Context.Features.AudioBackbone.Should().Be("maest");
        harness.Context.Features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id).Should().BeFalse();
    }

    [Fact]
    public void Run_legacyDualChoice_explainsHistoricalBehaviorAndConfirmsOneModel()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter().Enter());
        harness.Context.Features = new FeatureSelection
        {
            Enabled = ["audio-tagging-fast", "audio-tagging-deep", "autotagging"],
        };

        FeaturesScreen.Run(harness.Shell, new ResourceCalculator()).Should().Be(ScreenOutcome.Continue);

        harness.Output.Should().Contain("This saved setup selected both models");
        harness.Context.Features.HasLegacyDualAudioSelection.Should().BeFalse();
        harness.Context.Features.AudioBackbone.Should().Be("effnet");
    }

}
