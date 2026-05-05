using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.UpdateDir;

public class UpdateDirRequestValidator : Validator<UpdateDirRequest>
{
    public UpdateDirRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("New name cannot be empty")
            .MaximumLength(ValidationConstants.StringLengths.MediumString);

        RuleFor(x => x.DirectoryId).NotEmpty().WithMessage("Directory ID cannot be empty");
    }
}