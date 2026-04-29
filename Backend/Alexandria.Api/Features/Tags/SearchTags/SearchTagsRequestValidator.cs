using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Tags.SearchTags;

public sealed class SearchTagsRequestValidator
    : Validator<SearchTagsRequest>
{
    public SearchTagsRequestValidator()
    {
        // Optional GUIDs must not be empty if provided
        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty)
            .When(x => x.CreatedBy.HasValue)
            .WithMessage("CreatedBy cannot be an empty GUID.");

        RuleFor(x => x.UpdatedBy)
            .NotEqual(Guid.Empty)
            .When(x => x.UpdatedBy.HasValue)
            .WithMessage("UpdatedBy cannot be an empty GUID.");

        RuleFor(x => x.ExcludeOnFile)
            .NotEqual(Guid.Empty)
            .When(x => x.ExcludeOnFile.HasValue)
            .WithMessage("ExcludeOnFile cannot be an empty GUID.");

        // Date ranges
        RuleFor(x => x)
            .Must(x => !x.CreatedAfter.HasValue || !x.CreatedBefore.HasValue || x.CreatedAfter <= x.CreatedBefore)
            .WithMessage("CreatedAfter cannot be later than CreatedBefore.");

        RuleFor(x => x)
            .Must(x => !x.UpdatedAfter.HasValue || !x.UpdatedBefore.HasValue || x.UpdatedAfter <= x.UpdatedBefore)
            .WithMessage("UpdatedAfter cannot be later than UpdatedBefore.");

        // Optional NameContains length check
        RuleFor(x => x.NameContains)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.NameContains))
            .WithMessage("NameContains cannot exceed 100 characters.");

        // Pagination
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page must be 0 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}