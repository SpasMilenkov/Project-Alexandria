using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.SearchDirectory;

public class SearchDirectoryRequestValidator : Validator<SearchDirectoryRequest>
{
    public SearchDirectoryRequestValidator()
    {
        // Pagination validation
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page number must be non-negative");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        // Text search validation
        RuleFor(x => x.NameContains)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.NameContains))
            .WithMessage("Search term cannot exceed 200 characters");

        // Date range validation - CreatedAt
        RuleFor(x => x)
            .Must(x => !x.CreatedAfter.HasValue || !x.CreatedBefore.HasValue || 
                       x.CreatedAfter.Value <= x.CreatedBefore.Value)
            .WithMessage("CreatedAfter must be before or equal to CreatedBefore")
            .When(x => x.CreatedAfter.HasValue && x.CreatedBefore.HasValue);

        // Date range validation - UpdatedAt
        RuleFor(x => x)
            .Must(x => !x.UpdatedAfter.HasValue || !x.UpdatedBefore.HasValue || 
                       x.UpdatedAfter.Value <= x.UpdatedBefore.Value)
            .WithMessage("UpdatedAfter must be before or equal to UpdatedBefore")
            .When(x => x.UpdatedAfter.HasValue && x.UpdatedBefore.HasValue);

        // Prevent future dates
        RuleFor(x => x.CreatedBefore)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.CreatedBefore.HasValue)
            .WithMessage("CreatedBefore cannot be in the future");

        RuleFor(x => x.CreatedAfter)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.CreatedAfter.HasValue)
            .WithMessage("CreatedAfter cannot be in the future");

        RuleFor(x => x.UpdatedBefore)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.UpdatedBefore.HasValue)
            .WithMessage("UpdatedBefore cannot be in the future");

        RuleFor(x => x.UpdatedAfter)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.UpdatedAfter.HasValue)
            .WithMessage("UpdatedAfter cannot be in the future");

        RuleFor(x => x.DeletedAt)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.DeletedAt.HasValue)
            .WithMessage("DeletedAt cannot be in the future");

        // Logical validation - if searching for deleted items, can't also filter by non-deleted
        RuleFor(x => x)
            .Must(x => !(x.IsDeleted && x.DeletedAt.HasValue))
            .WithMessage("Cannot use both IsDeleted=true and specify a DeletedAt date. Use one or the other.");

        // GUIDs validation (optional - only if you want to ensure they're not empty)
        RuleFor(x => x.DirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.DirectoryId.HasValue)
            .WithMessage("DirectoryId cannot be an empty GUID");

        RuleFor(x => x.ParentDirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.ParentDirectoryId.HasValue)
            .WithMessage("ParentDirectoryId cannot be an empty GUID");

        RuleFor(x => x.OwnerId)
            .NotEqual(Guid.Empty)
            .When(x => x.OwnerId.HasValue)
            .WithMessage("OwnerId cannot be an empty GUID");

        // Sort validation (ensure enum is valid)
        RuleFor(x => x.SortBy)
            .IsInEnum()
            .WithMessage("Invalid sort field");

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("Invalid sort direction");
    }
}
