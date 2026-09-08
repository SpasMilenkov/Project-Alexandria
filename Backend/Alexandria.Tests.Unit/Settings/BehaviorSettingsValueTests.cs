using System.Text.Json;
using Alexandria.Common.Settings.Values;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Settings;

public class BehaviorSettingsValueTests
{
    [Fact]
    public void defaults_automaticMetadataOverwrite_isOff()
    {
        new BehaviorSettingsValue().AllowAutomaticMetadataOverwrite.Should().BeFalse();
    }

    [Fact]
    public void storedJson_withFlag_readsBack()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        const string stored = """{"skipDeleteConfirmation":false,"allowAutomaticMetadataOverwrite":true}""";

        var value = JsonSerializer.Deserialize<BehaviorSettingsValue>(stored, options);

        value.Should().NotBeNull();
        value!.AllowAutomaticMetadataOverwrite.Should().BeTrue();
        value.AllowAutoTagRegression.Should().BeFalse();
    }
}