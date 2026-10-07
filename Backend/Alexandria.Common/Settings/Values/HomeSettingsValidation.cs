using System.ComponentModel.DataAnnotations;

namespace Alexandria.Common.Settings.Values;

public static class HomeSettingsValidation
{
    public static IEnumerable<ValidationResult> Validate(HomeSettingsValue value)
    {
        if (value.SchemaVersion != HomeSettingsValue.CurrentSchemaVersion)
            yield return Error("Unsupported Home settings version.", nameof(value.SchemaVersion));

        foreach (var error in ValidateLayout(value.Desktop, nameof(value.Desktop), 12))
            yield return error;

        if (value.UseSeparateMobileLayout && value.Mobile is null)
            yield return Error("A separate mobile layout is required.", nameof(value.Mobile));

        if (value.Mobile is null) yield break;
        foreach (var error in ValidateLayout(value.Mobile, nameof(value.Mobile), 4))
            yield return error;
    }

    public static HomeSettingsValue NormalizeStored(HomeSettingsValue value)
    {
        // A newer document must stay recognizable so older clients cannot overwrite it.
        if (value.SchemaVersion != HomeSettingsValue.CurrentSchemaVersion)
            return value;

        value.Desktop = NormalizeLayout(value.Desktop ?? HomeSettingsValue.CreateDefaultLayout(), 12);

        if (value.Mobile is not null)
            value.Mobile = NormalizeLayout(value.Mobile, 4);
        else if (value.UseSeparateMobileLayout)
            value.UseSeparateMobileLayout = false;

        return value;
    }

    private static IEnumerable<ValidationResult> ValidateLayout(HomeLayoutValue? layout, string path, int columns)
    {
        if (layout?.Widgets is null)
        {
            yield return Error("A widget list is required.", path);
            yield break;
        }

        if (layout.Widgets.Count > HomeSettingsValue.MaxWidgetsPerLayout)
            yield return Error("A layout can contain at most 32 widgets.", $"{path}.Widgets");

        if (layout.Columns != columns)
            yield return Error($"This layout requires {columns} columns.", $"{path}.Columns");

        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < layout.Widgets.Count; i++)
        {
            var widget = layout.Widgets[i];
            var widgetPath = $"{path}.Widgets[{i}]";

            foreach (var error in ValidateWidget(widget, widgetPath, columns))
                yield return error;

            if (widget is not null && !ids.Add(widget.InstanceId))
                yield return Error("Widget instance IDs must be unique within a layout.", widgetPath);

            if (widget is null)
                continue;

            for (var previous = 0; previous < i; previous++)
            {
                var other = layout.Widgets[previous];

                if (other is not null && Overlaps(widget, other))
                {
                    yield return Error("Widgets cannot overlap.", widgetPath);
                    break;
                }
            }
        }
    }

    private static IEnumerable<ValidationResult> ValidateWidget(
        HomeWidgetValue? widget, string path, int columns, bool validateTimeZone = true)
    {
        if (widget is null)
        {
            yield return Error("A widget is required.", path);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(widget.InstanceId) || widget.InstanceId.Length > 64 ||
            widget.InstanceId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            yield return Error("Use a widget ID of 1–64 letters, digits, hyphens or underscores.",
                $"{path}.InstanceId");

        if (!Enum.IsDefined(widget.Type))
            yield return Error("Unknown widget type.", $"{path}.Type");

        if (!Enum.IsDefined(widget.Size))
            yield return Error("Unknown widget size.", $"{path}.Size");

        if (widget.Width < 1 || widget.Width > columns)
            yield return Error("Widget width must fit the grid.", $"{path}.Width");

        if (widget.Height < 1 || widget.Height > HomeSettingsValue.MaxWidgetHeight)
            yield return Error("Choose a supported widget height.", $"{path}.Height");

        if (widget.X < 0 || widget.X >= columns || (long)widget.X + widget.Width > columns)
            yield return Error("Widget column must fit the grid.", $"{path}.X");

        if (widget.Y < 0 || widget.Y >= HomeSettingsValue.MaxGridRows ||
            (long)widget.Y + widget.Height > HomeSettingsValue.MaxGridRows)
            yield return Error("Widget row must fit the grid.", $"{path}.Y");

        if (widget.Options is null)
        {
            yield return Error("Widget options are required.", $"{path}.Options");
            yield break;
        }

        if (validateTimeZone && !HomeTimeZones.IsSupported(widget.Options.TimeZone))
            yield return Error("Choose a supported time zone.", $"{path}.Options.TimeZone");

        if (!Enum.IsDefined(widget.Options.ShortcutGroup))
            yield return Error("Choose a supported shortcut group.", $"{path}.Options.ShortcutGroup");
    }

    private static HomeLayoutValue NormalizeLayout(HomeLayoutValue layout, int columns)
    {
        if (layout.Columns != columns)
            return HomeSettingsValue.CreateDefaultLayout(columns);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var retained = new List<HomeWidgetValue>();

        foreach (var widget in layout.Widgets ?? [])
        {
            if (ValidateWidget(widget, "Widget", columns, validateTimeZone: false).Any() ||
                !ids.Add(widget.InstanceId) || retained.Any(other => Overlaps(widget, other)))
                continue;

            retained.Add(widget);

            if (retained.Count == HomeSettingsValue.MaxWidgetsPerLayout)
                break;
        }

        layout.Widgets = retained;

        return layout;
    }

    private static bool Overlaps(HomeWidgetValue first, HomeWidgetValue second)
        => (long)first.X < (long)second.X + second.Width &&
           (long)first.X + first.Width > second.X &&
           (long)first.Y < (long)second.Y + second.Height &&
           (long)first.Y + first.Height > second.Y;

    private static ValidationResult Error(string message, string path) => new(message, [path]);
}