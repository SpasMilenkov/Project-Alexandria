using FastEndpoints;
using FluentValidation;

namespace API.Features.Users.GetUserStorageUsage;


sealed class GetUserStorageUsageRequestValidator : Validator<GetUserStorageUsageRequest>
{
    public GetUserStorageUsageRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
