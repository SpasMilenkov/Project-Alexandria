using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Previews.DeletePreviewsByFile;

public class DeletePreviewsByFileRequestValidator : Validator<DeletePreviewsByFileRequest>
{
    public DeletePreviewsByFileRequestValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().WithMessage("A valid file must be provided.");
    }
}