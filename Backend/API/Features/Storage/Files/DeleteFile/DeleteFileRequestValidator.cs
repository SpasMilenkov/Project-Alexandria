using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.DeleteFile;

public class DeleteFileRequestValidator : Validator<DeleteFileRequest>
{
    public DeleteFileRequestValidator()
    {
        RuleFor(x => x.Ids)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one file must be specified.");

        RuleForEach(x => x.Ids)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        RuleFor(x => x.Ids)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("File IDs must be unique.");
    }
}