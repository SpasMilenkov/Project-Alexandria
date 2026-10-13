using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeLibraryOptions : IValidatableObject
{
    [EnumDataType(typeof(HomeLibraryStyle))]
    public HomeLibraryStyle LibraryStyle { get; set; } = HomeLibraryStyle.Launchpad;

    [EnumDataType(typeof(HomeShortcutGroup))]
    public HomeShortcutGroup ShortcutGroup { get; set; } = HomeShortcutGroup.Files;

    [Required]
    [MaxLength(8)]
    public List<HomeLibraryShortcut> Shortcuts { get; set; } = [];

    [EnumDataType(typeof(HomeLibraryDestination))]
    public HomeLibraryDestination? FeaturedDestination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ShortcutGroup == HomeShortcutGroup.Custom && Shortcuts.Count == 0)
            yield return new ValidationResult("Choose between one and eight destinations.", [nameof(Shortcuts)]);

        if (ShortcutGroup != HomeShortcutGroup.Custom && Shortcuts.Count > 0)
            yield return new ValidationResult("Custom shortcuts require the Custom preset.", [nameof(Shortcuts)]);

        var selected = new HashSet<HomeLibraryDestination>();

        for (var index = 0; index < Shortcuts.Count; index++)
        {
            var shortcut = Shortcuts[index];
            var path = $"shortcuts[{index}]";

            if (shortcut is null)
            {
                yield return new ValidationResult("A shortcut is required.", [path]);
                continue;
            }

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(shortcut, new ValidationContext(shortcut), results, validateAllProperties: true);

            foreach (var result in results)
            {
                yield return new ValidationResult(result.ErrorMessage,
                    result.MemberNames.Select(member => $"{path}.{char.ToLowerInvariant(member[0])}{member[1..]}"));
            }

            if (!selected.Add(shortcut.Destination))
                yield return new ValidationResult("Choose each destination only once.", [$"{path}.destination"]);
        }

        if (ShortcutGroup != HomeShortcutGroup.Custom)
            selected = HomeLibraryPresets.Destinations(ShortcutGroup).ToHashSet();

        if (FeaturedDestination.HasValue && !selected.Contains(FeaturedDestination.Value))
            yield return new ValidationResult("The featured destination must be selected.", [nameof(FeaturedDestination)]);
    }
}
