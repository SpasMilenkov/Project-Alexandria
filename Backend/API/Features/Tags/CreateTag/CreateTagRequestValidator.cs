using FastEndpoints;
using FluentValidation;
using Models;

namespace API.Features.Tags.CreateTag;

public class CreateTagRequestValidator : Validator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tag name cannot be empty")
            .MaximumLength(ValidationConstants.StringLengths.MediumString)
            .WithMessage($"Tag name cannot exceed{ValidationConstants.StringLengths.MediumString}");

        RuleFor(x => x.Color)
            .NotEmpty()
            .WithMessage("Tag color cannot be empty.")
            .MaximumLength(7)
            .WithMessage("Tag color must be a valid 7-character hex color (e.g. #A1B2C3).")
            .Matches("^#[0-9a-fA-F]{6}$")
            .WithMessage("Tag color must be a valid hex color in the format #RRGGBB.");

        RuleFor(x => x.Icon)
            .NotEmpty()
            .WithMessage("Tag icon cannot be empty")
            .MaximumLength(ValidationConstants.StringLengths.ShortString)
            .WithMessage($"Icon length cannot exceed {ValidationConstants.StringLengths.ShortString}");

        RuleFor(x => x.Description)
            .MaximumLength(ValidationConstants.StringLengths.MediumString)
            .WithMessage($"Tag description cannot exceed {ValidationConstants.StringLengths.MediumString} characters");
    }
}