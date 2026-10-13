using System.Text.Json;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models;

namespace Alexandria.Services.User.Widgets;

internal static class HomeWidgetChanges
{
    public static void Apply(HomeWidget widget, JsonElement changes, string path)
    {
        foreach (var property in changes.EnumerateObject())
        {
            switch (property.Name)
            {
                case "x":
                    widget.X = ReadInteger(property, path);
                    break;
                case "y":
                    widget.Y = ReadInteger(property, path);
                    break;
                case "width":
                    widget.Width = ReadInteger(property, path);
                    break;
                case "height":
                    widget.Height = ReadInteger(property, path);
                    break;
                case "showTitle":
                    widget.ShowTitle = ReadNullableBoolean(property, path);
                    break;
                case "options":
                    widget.OptionsJson = HomeWidgetOptionsCodec.Merge(widget.Type, widget.OptionsVersion,
                        widget.OptionsJson, property.Value, path).GetRawText();
                    break;
                default:
                    throw HomeWidgetOptionsCodec.Error($"{path}.{property.Name}", "This widget field cannot be changed.");
            }
        }
    }

    public static void Apply(HomePreferencesValue preferences, JsonElement changes)
    {
        if (changes.ValueKind == JsonValueKind.Undefined)
            return;

        foreach (var property in changes.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                throw HomeWidgetOptionsCodec.Error($"Preferences.{property.Name}", "A boolean is required.");

            switch (property.Name)
            {
                case "useSeparateMobileLayout":
                    preferences.UseSeparateMobileLayout = property.Value.GetBoolean();
                    break;
                case "showWidgetTitles":
                    preferences.ShowWidgetTitles = property.Value.GetBoolean();
                    break;
                case "showHeaderGreeting":
                    preferences.ShowHeaderGreeting = property.Value.GetBoolean();
                    break;
                default:
                    throw HomeWidgetOptionsCodec.Error($"Preferences.{property.Name}", "Unknown Home preference.");
            }
        }
    }

    private static int ReadInteger(JsonProperty property, string path)
    {
        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var value))
            throw HomeWidgetOptionsCodec.Error($"{path}.{property.Name}", "An integer is required.");

        return value;
    }

    private static bool? ReadNullableBoolean(JsonProperty property, string path)
    {
        if (property.Value.ValueKind == JsonValueKind.Null)
            return null;

        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
            throw HomeWidgetOptionsCodec.Error($"{path}.{property.Name}", "A boolean or null is required.");

        return property.Value.GetBoolean();
    }
}
