
using API.Features.Users.DeleteUser;
using FastEndpoints;
using FluentValidation;

sealed class DeleteUserRequestValidator : Validator<DeleteUserRequest>
{
    public DeleteUserRequestValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("At least one user ID is required.")
            .Must(ids => ids.Length <= 100).WithMessage("Cannot delete more than 100 users in a single request.")
            .Must(ids => ids.Distinct().Count() == ids.Length).WithMessage("Duplicate user IDs are not allowed.");

        RuleForEach(x => x.UserIds)
            .NotEmpty().WithMessage("User IDs cannot be empty GUIDs.");
    }
}
