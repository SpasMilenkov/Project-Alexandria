using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.SearchDirectory;

public class SearchDirectoryRequestValidator : Validator<SearchDirectoryRequest>
{
    public SearchDirectoryRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page number must be non-negative");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.NameContains)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.NameContains))
            .WithMessage("Search term cannot exceed 200 characters");

        RuleFor(x => x)
            .Must(x => x.CreatedAfter!.Value <= x.CreatedBefore!.Value)
            .When(x => x.CreatedAfter.HasValue && x.CreatedBefore.HasValue)
            .WithMessage("CreatedAfter must be before or equal to CreatedBefore");

        RuleFor(x => x)
            .Must(x => x.UpdatedAfter!.Value <= x.UpdatedBefore!.Value)
            .When(x => x.UpdatedAfter.HasValue && x.UpdatedBefore.HasValue)
            .WithMessage("UpdatedAfter must be before or equal to UpdatedBefore");

        RuleFor(x => x)
            .Must(x => x.DeletedAfter!.Value <= x.DeletedBefore!.Value)
            .When(x => x.DeletedAfter.HasValue && x.DeletedBefore.HasValue)
            .WithMessage("DeletedAfter must be before or equal to DeletedBefore");

        RuleFor(x => x.ParentDirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.ParentDirectoryId.HasValue)
            .WithMessage("ParentDirectoryId cannot be an empty GUID");

        RuleFor(x => x.SortBy)
            .IsInEnum()
            .WithMessage("Invalid sort field");

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("Invalid sort direction");
    }
}
