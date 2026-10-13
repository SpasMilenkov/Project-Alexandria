using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeLibraryShortcut : IValidatableObject
{
    private string? _label;

    [JsonRequired]
    [EnumDataType(typeof(HomeLibraryDestination))]
    public HomeLibraryDestination Destination { get; set; }

    [StringLength(40, MinimumLength = 1)]
    public string? Label
    {
        get => _label;
        set => _label = value?.Trim();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Label?.Any(char.IsControl) == true)
            yield return new ValidationResult("Use a single-line label without control characters.", [nameof(Label)]);
    }
}
