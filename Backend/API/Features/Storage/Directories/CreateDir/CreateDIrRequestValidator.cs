using FastEndpoints;
using FluentValidation;
using Models;

namespace API.Features.Storage.Directories.CreateDir;

public class CreateDIrRequestValidator : Validator<CreateDirRequest>
{
    public CreateDIrRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cannot create directory with an empty name")
            .MaximumLength(ValidationConstants.StringLengths.MediumString);
    }
}