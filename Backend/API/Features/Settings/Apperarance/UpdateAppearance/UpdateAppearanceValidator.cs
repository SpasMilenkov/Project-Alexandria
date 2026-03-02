using FastEndpoints;
using FluentValidation;

namespace API.Features.Settings.Apperarance.UpdateAppearance;

public class UpdateAppearanceValidator : Validator<UpdateAppearanceRequest>
{
    private static readonly string[] ValidColors =
    [
        "red", "orange", "amber", "yellow", "lime", "green",
            "emerald", "teal", "cyan", "sky", "blue", "indigo",
            "violet", "purple", "fuchsia", "pink", "rose"
    ];

    private static readonly string[] ValidBackgrounds =
    [
        "system", "parchment", "cream", "newsprint",
            "linen", "midnight", "charcoal", "ink", "cool"
    ];

    public UpdateAppearanceValidator()
    {
        RuleFor(x => x.AccentColor)
            .NotEmpty()
            .Must(v => ValidColors.Contains(v))
            .WithMessage("Invalid accent color.");

        RuleFor(x => x.BackgroundColor)
            .NotEmpty()
            .Must(v => ValidBackgrounds.Contains(v))
            .WithMessage("Invalid background.");

        RuleFor(x => x.BackgroundImageOpacity)
            .InclusiveBetween(0.1, 0.65);

        RuleFor(x => x.GridIconSize)
            .InclusiveBetween(12, 64);

        RuleFor(x => x.ListIconSize)
            .InclusiveBetween(12, 64);
    }
}
