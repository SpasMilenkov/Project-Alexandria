using System.Text.Json;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models.Enumerators.Widgets;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Settings;

public class HomeLibraryOptionsTests
{
    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void Normalize_valid_styles_and_groups_round_trip_as_numbers(int style, int group)
    {
        var options = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1,
            Json($$"""{"libraryStyle":{{style}},"shortcutGroup":{{group}}}"""), "Widget");

        options.GetProperty("libraryStyle").GetInt32().Should().Be(style);
        options.GetProperty("shortcutGroup").GetInt32().Should().Be(group);
        options.GetProperty("shortcuts").GetArrayLength().Should().Be(0);
        options.GetProperty("featuredDestination").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void NormalizeStored_existing_group_gets_default_style_without_changing_group()
    {
        var options = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.LibraryShortcuts, 1,
            Json("""{"shortcutGroup":1}"""), "Widget");

        options.GetProperty("libraryStyle").GetInt32().Should().Be((int)HomeLibraryStyle.Launchpad);
        options.GetProperty("shortcutGroup").GetInt32().Should().Be((int)HomeShortcutGroup.Media);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"libraryStyle\":-1}")]
    [InlineData("{\"libraryStyle\":4}")]
    [InlineData("{\"libraryStyle\":2147483648}")]
    [InlineData("{\"libraryStyle\":1.5}")]
    [InlineData("{\"libraryStyle\":null}")]
    [InlineData("{\"libraryStyle\":\"1\"}")]
    [InlineData("{\"libraryStyle\":\"Directory\"}")]
    [InlineData("{\"libraryStyle\":true}")]
    [InlineData("{\"libraryStyle\":{}}")]
    [InlineData("{\"libraryStyle\":[]}")]
    [InlineData("{\"libraryStyle\":0,\"libraryStyle\":1}")]
    [InlineData("{\"LibraryStyle\":1}")]
    [InlineData("{\"shortcutGroup\":4}")]
    [InlineData("{\"shortcutGroup\":null}")]
    [InlineData("{\"libraryStyle\":1,\"to\":\"javascript:alert(1)\"}")]
    [InlineData("{\"libraryStyle\":1,\"clockStyle\":0}")]
    public void Normalize_invalid_library_options_are_rejected(string json)
    {
        var action = () => HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1,
            Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Fact]
    public void Merge_style_change_preserves_group_and_group_change_preserves_style()
    {
        var styleEdit = HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1,
            """{"shortcutGroup":1}""", Json("""{"libraryStyle":2}"""), "Widget");
        var groupEdit = HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1,
            styleEdit.GetRawText(), Json("""{"shortcutGroup":0}"""), "Widget");

        styleEdit.GetProperty("libraryStyle").GetInt32().Should().Be(2);
        styleEdit.GetProperty("shortcutGroup").GetInt32().Should().Be(1);
        groupEdit.GetProperty("libraryStyle").GetInt32().Should().Be(2);
        groupEdit.GetProperty("shortcutGroup").GetInt32().Should().Be(0);
    }

    [Fact]
    public void Normalize_custom_selection_preserves_order_trims_labels_and_round_trips_feature()
    {
        var normalized = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1,
            Json("""{"shortcutGroup":3,"shortcuts":[{"destination":13,"label":"  Highlights  "},{"destination":0}],"featuredDestination":0}"""),
            "Widget");
        var reloaded = HomeWidgetOptionsCodec.NormalizeStored(HomeWidgetType.LibraryShortcuts, 1, normalized, "Widget");

        reloaded.GetRawText().Should().Be(normalized.GetRawText());
        reloaded.GetProperty("shortcuts")[0].GetProperty("destination").GetInt32().Should().Be(13);
        reloaded.GetProperty("shortcuts")[0].GetProperty("label").GetString().Should().Be("Highlights");
        reloaded.GetProperty("shortcuts")[1].GetProperty("label").ValueKind.Should().Be(JsonValueKind.Null);
        reloaded.GetProperty("featuredDestination").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Normalize_custom_selection_accepts_boundary_sizes_and_maximum_label(int count)
    {
        var entries = string.Join(',', Enumerable.Range(0, count)
            .Select(destination => $$"""{"destination":{{destination}},"label":"{{new string('a', 40)}}"}"""));

        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1,
            Json($$"""{"shortcutGroup":3,"shortcuts":[{{entries}}]}"""), "Widget");

        result.GetProperty("shortcuts").GetArrayLength().Should().Be(count);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 11)]
    [InlineData(2, 8)]
    public void Normalize_preset_accepts_only_its_featured_members(int group, int destination)
    {
        var result = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1,
            Json($$"""{"shortcutGroup":{{group}},"featuredDestination":{{destination}}}"""), "Widget");

        result.GetProperty("featuredDestination").GetInt32().Should().Be(destination);
    }

    public static TheoryData<string> InvalidSelections => new()
    {
        """{"shortcutGroup":3}""",
        """{"shortcutGroup":3,"shortcuts":[]}""",
        """{"shortcuts":null}""",
        """{"shortcuts":{}}""",
        """{"shortcutGroup":3,"shortcuts":[null]}""",
        """{"shortcutGroup":3,"shortcuts":[[]]}""",
        """{"shortcutGroup":3,"shortcuts":[1]}""",
        """{"shortcutGroup":3,"shortcuts":[{}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":-1}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":14}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":2147483648}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":1.5}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":null}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":"0"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":true}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"destination":1}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"Destination":0}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"route":"/arbitrary"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"icon":"i-anything"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":false}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":{}}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":""}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"   "}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"a\nb"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"a\u0000b"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"a","label":"b"}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0},{"destination":0}]}""",
        """{"shortcuts":[{"destination":0}]}""",
        """{"shortcutGroup":1,"shortcuts":[{"destination":6}]}""",
        """{"shortcutGroup":2,"shortcuts":[{"destination":0}]}""",
        """{"shortcutGroup":3,"shortcuts":[{"destination":0}],"featuredDestination":1}""",
        """{"featuredDestination":13}""",
        """{"featuredDestination":-1}""",
        """{"featuredDestination":14}""",
        """{"featuredDestination":1.5}""",
        """{"featuredDestination":2147483648}""",
        """{"featuredDestination":"0"}""",
        """{"featuredDestination":true}""",
        """{"featuredDestination":[]}""",
        """{"featuredDestination":{}}""",
        $$"""{"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"{{new string('a', 41)}}"}]}""",
        $$"""{"shortcutGroup":3,"shortcuts":[{{string.Join(',', Enumerable.Range(0, 9).Select(id => $$"""{"destination":{{id}}}"""))}}]}"""
    };

    [Theory]
    [MemberData(nameof(InvalidSelections))]
    public void Normalize_invalid_nested_values_and_cross_field_invariants_are_rejected(string json)
    {
        var action = () => HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LibraryShortcuts, 1, Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Theory]
    [InlineData("{\"shortcutGroup\":0,\"shortcutGroup\":1}")]
    [InlineData("{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0,\"destination\":1}]}")]
    [InlineData("{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0,\"label\":\"a\",\"label\":\"b\"}]}")]
    public void Merge_duplicate_fields_are_rejected_before_json_nodes_can_discard_them(string json)
    {
        var action = () => HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1,
            """{"shortcutGroup":0}""", Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Fact]
    public void Merge_removing_a_feature_requires_clearing_it_in_the_same_patch()
    {
        const string stored = """{"shortcutGroup":3,"shortcuts":[{"destination":0},{"destination":6}],"featuredDestination":6}""";
        var invalid = () => HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1,
            stored, Json("""{"shortcuts":[{"destination":0}]}"""), "Widget");

        invalid.Should().Throw<HomeWidgetValidationException>();

        var changed = HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1, stored,
            Json("""{"shortcuts":[{"destination":0}],"featuredDestination":null}"""), "Widget");
        var styleOnly = HomeWidgetOptionsCodec.Merge(HomeWidgetType.LibraryShortcuts, 1, stored,
            Json("""{"libraryStyle":1}"""), "Widget");

        changed.GetProperty("featuredDestination").ValueKind.Should().Be(JsonValueKind.Null);
        styleOnly.GetProperty("shortcuts").GetArrayLength().Should().Be(2);
        styleOnly.GetProperty("featuredDestination").GetInt32().Should().Be(6);
    }
}
