using System.Text.Json;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Settings.Values;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models.Enumerators.Widgets;
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
        var options = JsonSerializer.SerializeToElement(new { timeZone = identifier }, new JsonSerializerOptions());

        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Clock, 1, options, "Widget");

        result.GetProperty("timeZone").GetString().Should().Be(identifier);
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
        var options = JsonSerializer.SerializeToElement(new { timeZone = identifier }, new JsonSerializerOptions());
        var action = () => HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Clock, 1, options, "Widget");

        action.Should().Throw<HomeWidgetValidationException>().Which.Errors.SelectMany(error => error.MemberNames)
            .Should().Contain("Widget.Options.timeZone");
    }

    [Fact]
    public void Validate_oversizedZone_rejectsInput()
    {
        HomeTimeZones.IsSupported(new string('a', HomeTimeZones.MaxIdentifierLength + 1)).Should().BeFalse();
    }

    [Fact]
    public void Normalize_unavailableStoredZone_preservesPreference_when_not_changing_zone()
    {
        var options = JsonSerializer.SerializeToElement(new { timeZone = "Europe/RemovedZone" }, new JsonSerializerOptions());
        var normalized = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Clock, 1, options, "Widget", validateTimeZone: false);

        normalized.GetProperty("timeZone").GetString().Should().Be("Europe/RemovedZone");
    }

    private static bool IsResolvableIanaZone(string identifier)
        => TimeZoneInfo.TryFindSystemTimeZoneById(identifier, out var zone) && zone.HasIanaId;
}
