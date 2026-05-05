using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.GetDir;

public class GetDirRequestValidator : Validator<GetDirRequest>
{
    public GetDirRequestValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty().WithMessage("A valid directory must be provided");
    }
}