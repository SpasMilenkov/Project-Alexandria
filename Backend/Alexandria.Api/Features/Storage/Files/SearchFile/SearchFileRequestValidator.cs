using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.SearchFile;

public class SearchFileRequestValidator : Validator<FileSearchQuery>
{
    private const int MaxTagIds = 20;

    public SearchFileRequestValidator()
    {
        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page number must be non-negative");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.NameContains)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.NameContains))
            .WithMessage("Search term cannot exceed 200 characters");

        RuleFor(x => x.MinSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinSize.HasValue)
            .WithMessage("MinSize must be non-negative");

        RuleFor(x => x.MaxSize)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxSize.HasValue)
            .WithMessage("MaxSize must be non-negative");

        RuleFor(x => x)
            .Must(x => x.MinSize!.Value <= x.MaxSize!.Value)
            .When(x => x.MinSize.HasValue && x.MaxSize.HasValue)
            .WithMessage("MinSize must be less than or equal to MaxSize");

        RuleFor(x => x.MimeType)
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.MimeType))
            .WithMessage("MIME type cannot exceed 255 characters");

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

        RuleFor(x => x.TagIds)
            .Must(ids => ids.Count <= MaxTagIds)
            .WithMessage($"Search by at most {MaxTagIds} tags");

        RuleForEach(x => x.TagIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Tag ids cannot be empty GUIDs");

        RuleFor(x => x.ParentDirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.ParentDirectoryId.HasValue)
            .WithMessage("ParentDirectoryId cannot be an empty GUID");

        RuleFor(x => x.WithinDirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.WithinDirectoryId.HasValue)
            .WithMessage("WithinDirectoryId cannot be an empty GUID");

        RuleFor(x => x.SortBy)
            .IsInEnum()
            .WithMessage("Invalid sort field");

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("Invalid sort direction");
    }
}
