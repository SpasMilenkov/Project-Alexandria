using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.MoveDir;

public class MoveDirValidator : Validator<MoveDirRequest>
{
    public MoveDirValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty();
    }
}
