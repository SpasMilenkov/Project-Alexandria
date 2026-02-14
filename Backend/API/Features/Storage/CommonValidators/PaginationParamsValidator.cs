using DTO.Search;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.CommonValidators;

public class PaginationParamsValidator : Validator<PaginationParams>
{
    public PaginationParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .IsInEnum();

        RuleFor(x => x.SortDirection)
            .IsInEnum();
    }
}