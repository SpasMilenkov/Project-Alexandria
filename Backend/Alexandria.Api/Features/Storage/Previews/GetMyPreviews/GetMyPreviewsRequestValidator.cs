using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Previews.GetMyPreviews;

public class GetMyPreviewsRequestValidator : Validator<GetMyPreviewsRequest>
{
    public GetMyPreviewsRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("A valid file must be provided.")
            .When(x => x.FileId.HasValue);

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ValidationConstants.PaginationConstants.MaxPageSize)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}