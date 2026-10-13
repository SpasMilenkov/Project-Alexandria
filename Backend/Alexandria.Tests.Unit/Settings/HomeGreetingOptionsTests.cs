using System.Text.Json;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models.Enumerators.Widgets;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Settings;

public class HomeGreetingOptionsTests
{
    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    [Fact]
    public void Normalize_empty_options_supply_numeric_defaults_and_plain_text()
    {
        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1, Json("{}"), "Widget");

        result.EnumerateObject().Should().HaveCount(8);
        result.GetProperty("greetingStyle").GetInt32().Should().Be(0);
        result.GetProperty("greetingDecoration").GetInt32().Should().Be(1);
        result.GetProperty("greetingColorMode").GetInt32().Should().Be(0);
        result.GetProperty("greetingAlignment").GetInt32().Should().Be(0);
        result.GetProperty("greetingShowName").GetBoolean().Should().BeTrue();
        result.GetProperty("greetingShowDate").GetBoolean().Should().BeFalse();
        result.GetProperty("greetingHeading").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("greetingSubtitle").GetString().Should().Be("Everything in your library, arranged your way.");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(0, 2, 2)]
    [InlineData(1, 3, 0)]
    public void Normalize_valid_variants_round_trip_with_trimmed_text(int style, int decoration, int alignment)
    {
        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1,
            Json($$"""{"greetingStyle":{{style}},"greetingDecoration":{{decoration}},"greetingAlignment":{{alignment}},"greetingHeading":"  Welcome home  ","greetingSubtitle":"  <b>Stay curious</b>  ","greetingShowName":false,"greetingShowDate":true}"""), "Widget");
        var reloaded = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.Greeting, 1, result, "Widget");

        reloaded.GetRawText().Should().Be(result.GetRawText());
        result.GetProperty("greetingStyle").GetInt32().Should().Be(style);
        result.GetProperty("greetingDecoration").GetInt32().Should().Be(decoration);
        result.GetProperty("greetingAlignment").GetInt32().Should().Be(alignment);
        result.GetProperty("greetingHeading").GetString().Should().Be("Welcome home");
        result.GetProperty("greetingSubtitle").GetString().Should().Be("<b>Stay curious</b>");
        result.GetProperty("greetingShowName").GetBoolean().Should().BeFalse();
        result.GetProperty("greetingShowDate").GetBoolean().Should().BeTrue();
    }

    public static TheoryData<HomeGreetingDecoration> Decorations => new(Enum.GetValues<HomeGreetingDecoration>());

    [Theory]
    [MemberData(nameof(Decorations))]
    public void Normalize_each_decoration_survives_stored_read_and_partial_edit(HomeGreetingDecoration decoration)
    {
        var normalized = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1,
            Json($$"""{"greetingDecoration":{{(int)decoration}}}"""), "Widget");
        var reloaded = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.Greeting, 1, normalized, "Widget");
        var edited = HomeWidgetOptionsCodec.Merge(HomeWidgetType.Greeting, 1, reloaded.GetRawText(),
            Json("""{"greetingHeading":"Hello"}"""), "Widget");

        edited.GetProperty("greetingDecoration").GetInt32().Should().Be((int)decoration);
        edited.GetProperty("greetingHeading").GetString().Should().Be("Hello");
        edited.EnumerateObject().Should().HaveCount(8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Normalize_color_modes_round_trip_and_survive_partial_edits(int mode)
    {
        var normalized = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1,
            Json($$"""{"greetingDecoration":7,"greetingColorMode":{{mode}}}"""), "Widget");
        var reloaded = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.Greeting, 1, normalized, "Widget");
        var updated = HomeWidgetOptionsCodec.Merge(HomeWidgetType.Greeting, 1, reloaded.GetRawText(),
            Json("""{"greetingHeading":"Hello"}"""), "Widget");

        updated.GetProperty("greetingColorMode").GetInt32().Should().Be(mode);
        updated.GetProperty("greetingDecoration").GetInt32().Should().Be(7);
    }

    [Fact]
    public void NormalizeStored_existing_options_default_to_monochrome()
    {
        var normalized = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.Greeting, 1,
            Json("""{"greetingStyle":1,"greetingDecoration":8}"""), "Widget");

        normalized.GetProperty("greetingColorMode").GetInt32().Should().Be(0);
        normalized.GetProperty("greetingDecoration").GetInt32().Should().Be(8);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Normalize_blank_heading_means_automatic_and_empty_subtitle_is_allowed(string heading)
    {
        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1,
            Json($$"""{"greetingHeading":{{heading}},"greetingSubtitle":"  "}"""), "Widget");

        result.GetProperty("greetingHeading").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("greetingSubtitle").GetString().Should().BeEmpty();
    }

    public static TheoryData<string> InvalidOptions => new()
    {
        "null", "[]",
        """{"greetingStyle":-1}""", """{"greetingStyle":2}""",
        """{"greetingStyle":1.5}""", """{"greetingStyle":2147483648}""",
        """{"greetingStyle":"Welcome"}""", """{"greetingStyle":"1"}""",
        """{"greetingStyle":null}""", """{"greetingStyle":true}""",
        """{"greetingStyle":{}}""", """{"greetingStyle":[]}""",
        """{"greetingStyle":0,"greetingStyle":1}""", """{"GreetingStyle":1}""",
        """{"greetingDecoration":-1}""", """{"greetingDecoration":19}""",
        """{"greetingDecoration":"1"}""", """{"greetingDecoration":null}""",
        """{"greetingDecoration":4.5}""", """{"greetingDecoration":2147483648}""",
        """{"greetingDecoration":"SunPixel"}""", """{"greetingDecoration":"4"}""",
        """{"greetingDecoration":true}""", """{"greetingDecoration":{}}""",
        """{"greetingDecoration":[]}""", """{"greetingDecoration":4,"greetingDecoration":5}""",
        """{"greetingDecoration":4,"svg":"<svg/>"}""",
        """{"greetingDecoration":"https://example.com/art.svg"}""",
        """{"greetingColorMode":-1}""", """{"greetingColorMode":2}""",
        """{"greetingColorMode":"1"}""", """{"greetingColorMode":"Colored"}""",
        """{"greetingColorMode":null}""", """{"greetingColorMode":true}""",
        """{"greetingColorMode":1.5}""", """{"greetingColorMode":2147483648}""",
        """{"greetingColorMode":{}}""", """{"greetingColorMode":[]}""",
        """{"greetingColorMode":0,"greetingColorMode":1}""", """{"GreetingColorMode":1}""",
        """{"greetingColorMode":1,"color":"#ff0000"}""",
        """{"greetingAlignment":3}""", """{"greetingAlignment":"Left"}""",
        """{"greetingShowName":null}""", """{"greetingShowName":"true"}""",
        """{"greetingShowDate":1}""", """{"greetingShowDate":null}""",
        """{"greetingHeading":1}""", """{"greetingSubtitle":null}""",
        """{"greetingSubtitle":false}""", """{"greetingHeading":"a\nb"}""",
        """{"greetingSubtitle":"a\u0000b"}""", """{"greetingHeading":"\n"}""",
        """{"greetingSubtitle":"\t"}""", """{"greetingDecoration":1,"svg":"<svg/>"}""",
        """{"greetingStyle":1,"clockStyle":0}""",
        $$"""{"greetingHeading":"{{new string('a', 81)}}"}""",
        $$"""{"greetingSubtitle":"{{new string('a', 161)}}"}"""
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Normalize_invalid_values_are_rejected(string json)
    {
        var action = () => HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Greeting, 1, Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Fact]
    public void Merge_partial_edit_preserves_other_fields_and_accepts_maximum_text_lengths()
    {
        var original = Json($$"""{"greetingStyle":1,"greetingDecoration":3,"greetingHeading":"{{new string('a', 80)}}","greetingSubtitle":"{{new string('b', 160)}}"}""");
        var result = HomeWidgetOptionsCodec.Merge(HomeWidgetType.Greeting, 1, original.GetRawText(),
            Json("""{"greetingShowDate":true}"""), "Widget");

        result.GetProperty("greetingStyle").GetInt32().Should().Be(1);
        result.GetProperty("greetingDecoration").GetInt32().Should().Be(3);
        result.GetProperty("greetingHeading").GetString().Should().HaveLength(80);
        result.GetProperty("greetingSubtitle").GetString().Should().HaveLength(160);
        result.GetProperty("greetingShowDate").GetBoolean().Should().BeTrue();
    }
}
