using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Tags.SearchFileByTags;

public sealed class SearchFilesByTagsRequestValidator
    : Validator<SearchFilesByTagsRequest>
{
    public SearchFilesByTagsRequestValidator()
    {
        // Required collection
        RuleFor(x => x.TagIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one tag ID must be specified.");

        RuleForEach(x => x.TagIds)
            .NotEmpty()
            .WithMessage("Tag ID cannot be empty.");

        // MatchType must be one of the allowed values
        RuleFor(x => x.MatchType)
            .NotEmpty()
            .Must(mt => mt == "any" || mt == "all" || mt == "exact")
            .WithMessage("MatchType must be one of: 'any', 'all', or 'exact'.");

        // Pagination
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page must be 0 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        // File size filters
        RuleFor(x => x.MinFileSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinFileSize.HasValue)
            .WithMessage("MinFileSize cannot be negative.");

        RuleFor(x => x.MaxFileSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxFileSize.HasValue)
            .WithMessage("MaxFileSize cannot be negative.");

        RuleFor(x => x)
            .Must(x => !x.MinFileSize.HasValue || !x.MaxFileSize.HasValue || x.MinFileSize <= x.MaxFileSize)
            .WithMessage("MinFileSize cannot be greater than MaxFileSize.");

        // Dates
        RuleFor(x => x)
            .Must(x => !x.CreatedAfter.HasValue || !x.CreatedBefore.HasValue || x.CreatedAfter <= x.CreatedBefore)
            .WithMessage("CreatedAfter cannot be later than CreatedBefore.");

        // Optional MimeTypePrefix length check
        RuleFor(x => x.MimeTypePrefix)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.MimeTypePrefix))
            .WithMessage("MimeTypePrefix cannot exceed 50 characters.");

        // Optional UserId check (if provided, must not be empty GUID)
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .When(x => x.UserId.HasValue)
            .WithMessage("UserId cannot be an empty GUID.");
    }
}