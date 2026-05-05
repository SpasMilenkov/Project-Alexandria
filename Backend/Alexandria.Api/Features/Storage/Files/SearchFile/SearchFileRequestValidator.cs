using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.SearchFile;

public class SearchFileRequestValidator : Validator<FileSearchQuery>
{
    public SearchFileRequestValidator()
    {
        // Pagination validation
        RuleFor(x => x.CurrentPage)
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

        // File size validation
        RuleFor(x => x.MinSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinSize.HasValue)
            .WithMessage("MinSize must be non-negative");

        RuleFor(x => x.MaxSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxSize.HasValue)
            .WithMessage("MaxSize must be non-negative");

        RuleFor(x => x)
            .Must(x => !x.MinSize.HasValue || !x.MaxSize.HasValue || x.MinSize.Value <= x.MaxSize.Value)
            .WithMessage("MinSize must be less than or equal to MaxSize")
            .When(x => x.MinSize.HasValue && x.MaxSize.HasValue);

        // MIME type validation
        RuleFor(x => x.MimeType)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.MimeType))
            .WithMessage("MIME type cannot exceed 255 characters");

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

        RuleFor(x => x.DeletedBefore)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.DeletedBefore.HasValue)
            .WithMessage("DeletedAt cannot be in the future");

        RuleFor(x => x.DeletedAfter)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.DeletedAfter.HasValue)
            .WithMessage("DeletedAt cannot be in the future");

        // GUIDs validation
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

        // Sort validation
        RuleFor(x => x.SortBy)
            .IsInEnum()
            .WithMessage("Invalid sort field");

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("Invalid sort direction");
    }
}