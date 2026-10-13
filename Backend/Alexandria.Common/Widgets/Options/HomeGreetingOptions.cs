using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeGreetingOptions : IValidatableObject
{
    private string? _heading;
    private string _subtitle = "Everything in your library, arranged your way.";
    private bool _headingHasControls;
    private bool _subtitleHasControls;

    [EnumDataType(typeof(HomeGreetingStyle))]
    public HomeGreetingStyle GreetingStyle { get; set; } = HomeGreetingStyle.Minimal;

    [EnumDataType(typeof(HomeGreetingDecoration))]
    public HomeGreetingDecoration GreetingDecoration { get; set; } = HomeGreetingDecoration.WaveEmoji;

    [EnumDataType(typeof(HomeGreetingColorMode))]
    public HomeGreetingColorMode GreetingColorMode { get; set; } = HomeGreetingColorMode.Monochrome;

    [EnumDataType(typeof(HomeHorizontalAlignment))]
    public HomeHorizontalAlignment GreetingAlignment { get; set; } = HomeHorizontalAlignment.Left;

    public bool GreetingShowName { get; set; } = true;

    public bool GreetingShowDate { get; set; }

    [StringLength(80, MinimumLength = 1)]
    public string? GreetingHeading
    {
        get => _heading;
        set
        {
            _headingHasControls = value?.Any(char.IsControl) == true;
            _heading = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    [Required(AllowEmptyStrings = true), StringLength(160)]
    public string GreetingSubtitle
    {
        get => _subtitle;
        set
        {
            _subtitleHasControls = value?.Any(char.IsControl) == true;
            _subtitle = value?.Trim()!;
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (_headingHasControls)
            yield return new ValidationResult("Control characters are not allowed.", [nameof(GreetingHeading)]);

        if (_subtitleHasControls)
            yield return new ValidationResult("Control characters are not allowed.", [nameof(GreetingSubtitle)]);
    }
}
