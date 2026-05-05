using Alexandria.Data.Models;
using Alexandria.Dto.Users;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Users.GetUsers;

public class GetUsersValidator : Validator<UserQueryDto>
{
    public GetUsersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0).WithMessage("Page must be non-negative.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ValidationConstants.PaginationConstants.MaxPageSize)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .IsInEnum().WithMessage("Invalid sort field.");

        RuleFor(x => x.SortDirection)
            .IsInEnum().WithMessage("Invalid sort direction.");

        RuleFor(x => x.UserName)
            .MaximumLength(50).WithMessage("Username filter cannot exceed 50 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.UserName));

        RuleFor(x => x.UserEmail)
            .EmailAddress().WithMessage("Email filter must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.UserEmail));

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role specified.")
            .When(x => x.Role.HasValue);

        // Date range consistency checks
        When(x => x.CreatedAfter.HasValue && x.CreatedBefore.HasValue, () =>
            RuleFor(x => x.CreatedAfter)
                .LessThan(x => x.CreatedBefore).WithMessage("CreatedAfter must be earlier than CreatedBefore."));

        When(x => x.UpdatedAfter.HasValue && x.UpdatedBefore.HasValue, () =>
            RuleFor(x => x.UpdatedAfter)
                .LessThan(x => x.UpdatedBefore).WithMessage("UpdatedAfter must be earlier than UpdatedBefore."));

        When(x => x.DeletedAfter.HasValue && x.DeletedBefore.HasValue, () =>
            RuleFor(x => x.DeletedAfter)
                .LessThan(x => x.DeletedBefore).WithMessage("DeletedAfter must be earlier than DeletedBefore."));

        When(x => x.LockedOutAfter.HasValue && x.LockedOutBefore.HasValue, () =>
            RuleFor(x => x.LockedOutAfter)
                .LessThan(x => x.LockedOutBefore).WithMessage("LockedOutAfter must be earlier than LockedOutBefore."));

        // Mutual exclusivity of deletion filters
        RuleFor(x => x.ShowDeletedOnly)
            .Equal(false).WithMessage("ShowDeleted and ShowDeletedOnly cannot both be true.")
            .When(x => x.ShowDeleted && x.ShowDeletedOnly);
    }
}