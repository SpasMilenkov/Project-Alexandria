using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Settings.Values;
using Alexandria.Common.Widgets.Options;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets;

public static class HomeWidgetOptionsCodec
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    private static readonly FrozenDictionary<HomeWidgetType, Type> OptionTypes =
        new Dictionary<HomeWidgetType, Type>
        {
            [HomeWidgetType.Clock] = typeof(HomeClockOptions),
            [HomeWidgetType.LibraryShortcuts] = typeof(HomeLibraryOptions),
            [HomeWidgetType.Storage] = typeof(HomeStorageOptions),
            [HomeWidgetType.RecentFiles] = typeof(HomeRecentFilesOptions),
            [HomeWidgetType.QuickAccess] = typeof(HomeQuickAccessOptions),
            [HomeWidgetType.QuickUpload] = typeof(HomeQuickUploadOptions),
            [HomeWidgetType.Player] = typeof(HomePlayerOptions),
            [HomeWidgetType.SystemStatus] = typeof(HomeStatusOptions),
            [HomeWidgetType.TopSongs] = typeof(HomeEmptyOptions),
            [HomeWidgetType.RecentActivity] = typeof(HomeActivityOptions),
            [HomeWidgetType.TranscodingJobs] = typeof(HomeEmptyOptions),
            [HomeWidgetType.LatestRecap] = typeof(HomeRecapOptions),
            [HomeWidgetType.Collections] = typeof(HomeEmptyOptions),
            [HomeWidgetType.Countdown] = typeof(HomeCountdownOptions),
            [HomeWidgetType.FocusTimer] = typeof(HomeFocusOptions),
            [HomeWidgetType.DailyCover] = typeof(HomeDailyCoverOptions),
            [HomeWidgetType.Greeting] = typeof(HomeGreetingOptions)
        }.ToFrozenDictionary();

    public static JsonElement Normalize(HomeWidgetType type, int version, JsonElement options,
        string path, bool validateTimeZone = true)
    {
        if (!OptionTypes.TryGetValue(type, out var optionType))
            throw Error($"{path}.Type", "Unknown widget type.");

        if (version != CurrentVersion)
            throw Error($"{path}.OptionsVersion", "Unsupported widget options version.");

        RequireObject(options, $"{path}.Options");
        ValidateLibraryEntries(type, options, path);

        object value;

        try
        {
            value = JsonSerializer.Deserialize(options, optionType, JsonOptions)!;
        }
        catch (JsonException)
        {
            throw Error($"{path}.Options", "Widget options contain an unknown field or an invalid value.");
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);

        if (results.Count > 0)
        {
            throw new HomeWidgetValidationException(results.Select(result => new ValidationResult(
                result.ErrorMessage,
                result.MemberNames.Select(member => $"{path}.Options.{JsonNamingPolicy.CamelCase.ConvertName(member)}")))
                .ToList());
        }

        if (validateTimeZone && value is HomeClockOptions clock && !HomeTimeZones.IsSupported(clock.TimeZone))
            throw Error($"{path}.Options.timeZone", "Choose a supported time zone.");

        return JsonSerializer.SerializeToElement(value, optionType, JsonOptions);
    }

    public static JsonElement NormalizeStored(HomeWidgetType type, int version, JsonElement options, string path)
        => Normalize(type, version, RestoreRetiredStyle(type, options, path), path, validateTimeZone: false);

    public static JsonElement Merge(HomeWidgetType type, int version, string storedJson,
        JsonElement changes, string path)
    {
        RequireObject(changes, $"{path}.Options");
        ValidateLibraryEntries(type, changes, path);

        using var stored = JsonDocument.Parse(storedJson);
        var compatible = RestoreRetiredStyle(type, stored.RootElement, path);
        var merged = JsonNode.Parse(compatible.GetRawText())!.AsObject();

        foreach (var property in changes.EnumerateObject())
            merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());

        var element = JsonSerializer.SerializeToElement(merged, JsonOptions);

        return Normalize(type, version, element, path, changes.TryGetProperty("timeZone", out _));
    }

    private static JsonElement RestoreRetiredStyle(HomeWidgetType type, JsonElement options, string path)
    {
        RequireObject(options, $"{path}.Options");

        string property;
        int retiredValue;

        if (type == HomeWidgetType.FocusTimer)
        {
            property = "focusStyle";
            retiredValue = 3; // Hourglass was removed; Ring is the default.
        }
        else if (type == HomeWidgetType.Clock)
        {
            property = "clockStyle";
            retiredValue = 6; // Sky was removed; Minimal is the default.
        }
        else
        {
            return options;
        }

        if (!options.TryGetProperty(property, out var style) || style.ValueKind != JsonValueKind.Number ||
            !style.TryGetInt32(out var value) || value != retiredValue)
            return options;

        var compatible = JsonNode.Parse(options.GetRawText())!.AsObject();
        compatible[property] = 0;

        return JsonSerializer.SerializeToElement(compatible, JsonOptions);
    }

    public static void RequireObject(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "An object is required.");

        if (value.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
            throw Error(path, "Duplicate fields are not allowed.");
    }

    private static void ValidateLibraryEntries(HomeWidgetType type, JsonElement options, string path)
    {
        if (type != HomeWidgetType.LibraryShortcuts ||
            !options.TryGetProperty("shortcuts", out var shortcuts) || shortcuts.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;

        foreach (var shortcut in shortcuts.EnumerateArray())
        {
            RequireObject(shortcut, $"{path}.Options.shortcuts[{index}]");
            index++;
        }
    }

    public static HomeWidgetValidationException Error(string path, string message)
        => new([new ValidationResult(message, [path])]);
}
