using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Tags.AddTagsToFile;

public class AddTagsToFileRequestValidator : Validator<AddTagsToFileRequest>
{
    public AddTagsToFileRequestValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().WithMessage("File ID cannot be empty");

        RuleFor(x => x.TagIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one file must be specified.");

        RuleForEach(x => x.TagIds)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        RuleFor(x => x.TagIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("File IDs must be unique.");
    }
}