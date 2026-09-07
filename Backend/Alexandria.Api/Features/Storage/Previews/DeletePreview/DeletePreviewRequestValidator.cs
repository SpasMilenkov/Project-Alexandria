using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Previews.DeletePreview;

public class DeletePreviewRequestValidator : Validator<DeletePreviewRequest>
{
    public DeletePreviewRequestValidator()
    {
        RuleFor(x => x.PreviewId).NotEmpty().WithMessage("A valid preview must be provided.");
    }
}