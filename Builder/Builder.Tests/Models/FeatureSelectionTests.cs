using AwesomeAssertions;
using Builder.Models;

namespace Builder.Tests.Models;

public class FeatureSelectionTests
{
    [Fact]
    public void IsEnabled_unknownFeatureId_returnsFalse()
    {
        var sut = new FeatureSelection();

        sut.IsEnabled("no-such-feature").Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_togglesFeatureOnAndOff()
    {
        var sut = new FeatureSelection();

        sut.SetEnabled(FeatureCatalog.Monitoring.Id, true);
        sut.IsEnabled(FeatureCatalog.Monitoring.Id).Should().BeTrue();

        sut.SetEnabled(FeatureCatalog.Monitoring.Id, false);
        sut.IsEnabled(FeatureCatalog.Monitoring.Id).Should().BeFalse();
    }

    [Fact]
    public void FromDefaults_matchesCatalogDefaultStates()
    {
        var sut = FeatureSelection.FromDefaults();

        foreach (var feature in FeatureCatalog.All)
        {
            sut.IsEnabled(feature.Id).Should().Be(feature.DefaultEnabled,
                because: $"feature '{feature.Id}' default state must round-trip");
        }
    }

    [Fact]
    public void All_catalogEntries_haveNonEmptyCopy()
    {
        foreach (var feature in FeatureCatalog.All)
        {
            feature.Id.Should().NotBeNullOrWhiteSpace();
            feature.Label.Should().NotBeNullOrWhiteSpace();
            feature.Description.Should().NotBeNullOrWhiteSpace();
            feature.Cost.Should().NotBeNullOrWhiteSpace();
        }
    }
}
