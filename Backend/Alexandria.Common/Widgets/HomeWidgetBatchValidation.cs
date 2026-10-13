using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Alexandria.Common.Exceptions;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Widgets;
using Alexandria.Dto.Home;

namespace Alexandria.Common.Widgets;

public static class HomeWidgetBatchValidation
{
    public const int MaxWidgetsPerLayout = 32;
    public const int MaxRows = 256;
    public const int MaxHeight = 12;

    public static void ValidateRequest(HomeBoardChanges changes)
    {
        if (changes.Creates is null || changes.Updates is null || changes.Deletes is null)
            throw HomeWidgetOptionsCodec.Error("Widgets", "Create, update and delete lists are required.");

        if (changes.Creates.Count > 64 || changes.Updates.Count > 64 || changes.Deletes.Count > 64)
            throw HomeWidgetOptionsCodec.Error("Widgets", "A batch can change at most 64 widgets per operation.");

        var ids = new HashSet<Guid>();

        foreach (var widget in changes.Creates)
        {
            if (widget is null || widget.Id == Guid.Empty || !ids.Add(widget.Id))
                throw HomeWidgetOptionsCodec.Error("Creates", "Provide nonempty, distinct widget IDs across the batch.");
        }

        foreach (var update in changes.Updates)
        {
            if (update is null || update.Id == Guid.Empty || !ids.Add(update.Id))
                throw HomeWidgetOptionsCodec.Error("Updates", "Provide nonempty, distinct widget IDs across the batch.");

            HomeWidgetOptionsCodec.RequireObject(update.Changes, "Updates.Changes");
        }

        foreach (var id in changes.Deletes)
        {
            if (id == Guid.Empty || !ids.Add(id))
                throw HomeWidgetOptionsCodec.Error("Deletes", "Provide nonempty, distinct widget IDs across the batch.");
        }

        if (changes.Preferences.ValueKind != JsonValueKind.Undefined)
            HomeWidgetOptionsCodec.RequireObject(changes.Preferences, "Preferences");
    }

    public static void ValidateLayouts(IReadOnlyList<HomeWidget> widgets, HomePreferencesValue preferences)
    {
        var errors = new List<ValidationResult>();

        if (preferences.UseSeparateMobileLayout && !preferences.HasMobileLayout)
            errors.Add(new ValidationResult("Initialize a mobile layout before enabling it.", ["InitializeMobileLayout"]));

        foreach (var group in widgets.GroupBy(widget => widget.Layout))
        {
            if (!Enum.IsDefined(group.Key))
            {
                errors.Add(new ValidationResult("Unknown layout.", ["Layout"]));
                continue;
            }

            if (group.Count() > MaxWidgetsPerLayout)
                errors.Add(new ValidationResult("A layout can contain at most 32 widgets.", ["Widgets"]));

            var columns = 12;

            if (group.Key == HomeLayout.Mobile)
                columns = 4;

            var layout = group.ToList();

            for (var index = 0; index < layout.Count; index++)
            {
                var widget = layout[index];
                var path = $"Widgets[{widget.Id}]";

                if (!Enum.IsDefined(widget.Type))
                    errors.Add(new ValidationResult("Unknown widget type.", [$"{path}.Type"]));

                if (widget.Width < 1 || widget.Width > columns || widget.X < 0 ||
                    (long)widget.X + widget.Width > columns)
                    errors.Add(new ValidationResult("Widget width and column must fit the grid.", [$"{path}.Width"]));

                if (widget.Height < 1 || widget.Height > MaxHeight || widget.Y < 0 ||
                    (long)widget.Y + widget.Height > MaxRows)
                    errors.Add(new ValidationResult("Widget height and row must fit the grid.", [$"{path}.Height"]));

                if (layout.Take(index).Any(other => Overlaps(widget, other)))
                    errors.Add(new ValidationResult("Widgets cannot overlap.", [path]));
            }
        }

        if (errors.Count > 0)
            throw new HomeWidgetValidationException(errors);
    }

    private static bool Overlaps(HomeWidget first, HomeWidget second)
        => (long)first.X < (long)second.X + second.Width && (long)first.X + first.Width > second.X &&
           (long)first.Y < (long)second.Y + second.Height && (long)first.Y + first.Height > second.Y;
}
