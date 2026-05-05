using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Users.GetUserFileCount;

sealed class GetUserFileCountPerUserValidator : Validator<GetUserFileCountPerUserRequest>
{
    public GetUserFileCountPerUserValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}