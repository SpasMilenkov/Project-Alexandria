using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Tests.Support;
using Builder.UI.Shell.Screens;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class FeaturesScreenTests
{
    private static IEnumerable<string> EnabledIds(FeatureSelection features) =>
        FeatureCatalog.All.Where(f => features.IsEnabled(f.Id)).Select(f => f.Id);

    [Fact]
    public void Run_confirmImmediately_keepsCatalogDefaults()
    {
        using var h = new ShellHarness(new FakeKeys().Enter());

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
    public void Run_spaceOnDeepTagging_enablesIt()
    {
        // highlight order matches FeatureCatalog.All; Deep sits four rows down
        using var h = new ShellHarness(new FakeKeys().Down().Down().Down().Down().Space().Enter());

        FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        h.Context.Features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id).Should().BeTrue();
    }

    [Fact]
    public void Run_highlightingStreaming_showsItsComponents()
    {
        // Adaptive streaming sits two rows below the highlight start; the
        // detail pane re-renders on every move, so the component list for it
        // must appear before Enter confirms the defaults
        using var h = new ShellHarness(new FakeKeys().Down().Down().Enter());

        var outcome = FeaturesScreen.Run(h.Shell, new ResourceCalculator());

        outcome.Should().Be(ScreenOutcome.Continue);
        h.Output.Should().Contain("Turns on:");
        h.Output.Should().Contain("Transpilation worker");
    }

    [Fact]
    public void Run_enablingMonitoring_growsServiceCountBySix()
    {
        var calculator = new ResourceCalculator();
        var before = calculator.CountServices(FeatureSelection.FromDefaults());

        // Monitoring is the last row: seven Downs, Space to switch on, Enter
        using var h = new ShellHarness(new FakeKeys().Down().Down().Down().Down().Down().Down().Down().Space().Enter());

        FeaturesScreen.Run(h.Shell, calculator);

        var after = calculator.CountServices(h.Context.Features);

        (after - before).Should().Be(6); // prometheus, grafana, cadvisor, exporter, alloy, loki
    }
}
