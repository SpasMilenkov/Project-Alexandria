using Alexandria.Common.Settings.Values;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Settings;

public class HomeTimeZoneTests
{
    [Fact]
    public void Identifiers_systemCatalogue_exposesResolvableIanaZonesAndSpecialOptions()
    {
        HomeTimeZones.Identifiers.Take(2).Should().Equal("local", "UTC");
        HomeTimeZones.Identifiers.Should().OnlyHaveUniqueItems();
        HomeTimeZones.Identifiers.Should().Contain("Asia/Kathmandu").And.Contain("Pacific/Chatham");
        HomeTimeZones.Identifiers.Skip(2).Should().BeInAscendingOrder(StringComparer.Ordinal);
        HomeTimeZones.Identifiers.Where(identifier => identifier != "local").Should()
            .OnlyContain(identifier => IsResolvableIanaZone(identifier));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("UTC")]
    [InlineData("Europe/Bucharest")]
    [InlineData("Asia/Kathmandu")]
    [InlineData("Pacific/Chatham")]
    [InlineData("US/Eastern")]
    [InlineData("Asia/Calcutta")]
    [InlineData("Etc/UTC")]
    public void Validate_resolvableZoneOrAlias_acceptsSettings(string identifier)
    {
        var settings = new HomeSettingsValue();
        settings.Desktop.Widgets[0].Options.TimeZone = identifier;

        HomeSettingsValidation.Validate(settings).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Europe/DefinitelyNotAPlace")]
    [InlineData("Eastern Standard Time")]
    [InlineData("+02:00")]
    [InlineData("../Europe/Bucharest")]
    public void Validate_unknownZoneOrNonIanaInput_reportsOptionPath(string? identifier)
    {
        var settings = new HomeSettingsValue();
        settings.Desktop.Widgets[0].Options.TimeZone = identifier!;

        HomeSettingsValidation.Validate(settings).SelectMany(error => error.MemberNames).Should()
            .Contain("Desktop.Widgets[0].Options.TimeZone");
    }

    [Fact]
    public void Validate_oversizedZone_rejectsInput()
    {
        HomeTimeZones.IsSupported(new string('a', HomeTimeZones.MaxIdentifierLength + 1)).Should().BeFalse();
    }

    [Fact]
    public void NormalizeStored_unavailableZone_preservesWidgetAndPreferenceInBothLayouts()
    {
        var settings = new HomeSettingsValue { Mobile = HomeSettingsValue.CreateDefaultLayout(4) };
        settings.Desktop.Widgets[0].Options.TimeZone = "Europe/RemovedZone";
        settings.Mobile.Widgets[0].Options.TimeZone = "Asia/RemovedZone";

        var normalized = HomeSettingsValidation.NormalizeStored(settings);

        normalized.Desktop.Widgets.Should().HaveCount(2);
        normalized.Mobile!.Widgets.Should().HaveCount(2);
        normalized.Desktop.Widgets[0].Options.TimeZone.Should().Be("Europe/RemovedZone");
        normalized.Mobile.Widgets[0].Options.TimeZone.Should().Be("Asia/RemovedZone");
        HomeSettingsValidation.Validate(normalized).Should().NotBeEmpty();
    }

    private static bool IsResolvableIanaZone(string identifier)
        => TimeZoneInfo.TryFindSystemTimeZoneById(identifier, out var zone) && zone.HasIanaId;
}