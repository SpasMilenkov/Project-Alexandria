using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.GetDir;

public class GetDirRequestValidator : Validator<GetDirRequest>
{
    public GetDirRequestValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty().WithMessage("A valid directory must be provided");
    }
}