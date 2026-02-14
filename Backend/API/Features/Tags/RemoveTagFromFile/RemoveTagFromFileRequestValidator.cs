using FastEndpoints;
using FluentValidation;

namespace API.Features.Tags.RemoveTagFromFile;

public class RemoveTagFromFileRequestValidator : Validator<RemoveTagFromFileRequest>
{
    public RemoveTagFromFileRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty()
            .WithMessage("File ID cannot be empty");

        RuleFor(x => x.TagId)
            .NotEmpty()
            .WithMessage("Tag ID cannot be empty");
    }
}