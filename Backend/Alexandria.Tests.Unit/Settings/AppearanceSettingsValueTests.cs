using System.Text.Json;
using Alexandria.Common.Settings.Values;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Settings;

public class AppearanceSettingsValueTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void defaults_tabIconColor_matchesAccent()
    {
        new AppearanceSettingsValue().TabIconColor.Should().Be("accent");
    }

    [Fact]
    public void storedJson_withoutTabIconColor_matchesAccent()
    {
        const string stored = """{"accentColor":"rose","backgroundColor":"parchment"}""";

        var value = JsonSerializer.Deserialize<AppearanceSettingsValue>(stored, Options);

        value.Should().NotBeNull();
        value!.AccentColor.Should().Be("rose");
        value.TabIconColor.Should().Be("accent");
    }

    [Fact]
    public void storedJson_withTabIconColor_readsBack()
    {
        const string stored = """{"accentColor":"rose","tabIconColor":"sky"}""";

        var value = JsonSerializer.Deserialize<AppearanceSettingsValue>(stored, Options);

        value!.TabIconColor.Should().Be("sky");
    }
}
