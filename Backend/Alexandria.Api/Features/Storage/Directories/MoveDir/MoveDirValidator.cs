using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.MoveDir;

public class MoveDirValidator : Validator<MoveDirRequest>
{
    public MoveDirValidator()
    {
        RuleFor(x => x.DirectoryIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one directory must be specified.");

        RuleForEach(x => x.DirectoryIds)
            .NotEmpty()
            .WithMessage("Directory ID cannot be empty.");

        RuleFor(x => x.DirectoryIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("Directory IDs must be unique.");

        RuleFor(x => x)
            .Must(x => x.DestinationId == null || !x.DirectoryIds.Contains(x.DestinationId ?? Guid.Empty))
            .WithMessage("Destination cannot be one of the source directories.");
    }
}