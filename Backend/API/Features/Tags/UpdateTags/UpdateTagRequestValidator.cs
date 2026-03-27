using FastEndpoints;
using FluentValidation;

namespace API.Features.Tags.UpdateTags;

public sealed class UpdateTagRequestValidator
    : Validator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        // TagId is required
        RuleFor(x => x.TagId)
            .NotEmpty()
            .WithMessage("TagId cannot be empty.");

        // At least one field must be provided to update
        RuleFor(x => x)
            .Must(HaveAtLeastOneFieldToUpdate)
            .WithMessage("At least one field must be provided to update.");

        // Optional Name
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Name must be between 1 and 100 characters.");
        });

        // Optional Icon
        When(x => x.Icon is not null, () =>
        {
            RuleFor(x => x.Icon!)
                .NotEmpty()
                .MaximumLength(50)
                .WithMessage("Icon must be between 1 and 50 characters.");
        });

        // Optional Color — must be a valid hex color
        When(x => x.Color is not null, () =>
        {
            RuleFor(x => x.Color)
                .NotEmpty()
                .WithMessage("Tag color cannot be empty.");
        });

        // Optional Description
        When(x => x.Description is not null, () =>
        {
            RuleFor(x => x.Description!)
                .MaximumLength(500)
                .WithMessage("Description cannot exceed 500 characters.");
        });
    }

    private static bool HaveAtLeastOneFieldToUpdate(UpdateTagRequest x)
        => x.Name is not null
           || x.Icon is not null
           || x.Color is not null
           || x.Description is not null;
}