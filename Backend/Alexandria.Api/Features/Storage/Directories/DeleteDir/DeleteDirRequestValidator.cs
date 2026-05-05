using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.DeleteDir;

public class DeleteDirRequestValidator : Validator<DeleteDirRequest>
{
    public DeleteDirRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("A valid directory must be provided");
    }
}