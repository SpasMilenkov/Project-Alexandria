using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.MoveDir;

public class MoveDirValidator : Validator<MoveDirRequest>
{
    public MoveDirValidator()
    {
        RuleFor(x => x.DirectoryIds).NotEmpty();
        RuleFor(x => x.DestinationId).NotEmpty();
    }
}