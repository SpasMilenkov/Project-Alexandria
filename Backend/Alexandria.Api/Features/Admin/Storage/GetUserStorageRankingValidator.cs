using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.Storage;

sealed class GetUserStorageRankingValidator : Validator<GetUserStorageRankingRequest>
{
    public GetUserStorageRankingValidator()
    {
        RuleFor(x => x.Top)
            .InclusiveBetween(1, 50).WithMessage("Top must be between 1 and 50.");
    }
}