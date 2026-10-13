using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeRecapOptions : IValidatableObject
{
    [EnumDataType(typeof(HomeRecapStyle))]
    public HomeRecapStyle RecapStyle { get; set; } = HomeRecapStyle.Poster;

    [EnumDataType(typeof(HomeRecapPeriod))]
    public HomeRecapPeriod RecapPeriod { get; set; } = HomeRecapPeriod.CurrentMonth;

    public string[]? RecapPalette { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RecapPalette is null) yield break;

        if (RecapPalette.Length != 3 || RecapPalette.Any(color =>
                color is null || color.Length != 7 || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$", RegexOptions.NonBacktracking)))
            yield return new ValidationResult("Choose three hexadecimal colors.", [nameof(RecapPalette)]);
    }
}
