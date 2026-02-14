using FastEndpoints;
using FluentValidation;
using Models;

namespace API.Features.Storage.Directories.GetRootDirectories;

public class GetRootDirRequestValidator
    : Validator<GetRootDirRequest>
{
    public GetRootDirRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(ValidationConstants.PaginationConstants.MaxPageSize);

        RuleFor(x => x.SortBy)
            .IsInEnum();

        RuleFor(x => x.SortDirection)
            .IsInEnum();
    }
}