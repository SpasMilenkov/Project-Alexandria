using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Settings.Behavior.UpdateBehavior;

public class UpdateBehaviorValidator : Validator<UpdateBehaviorRequest>
{
    public UpdateBehaviorValidator()
    {
        RuleFor(x => x.ToastLevel)
            .IsInEnum()
            .WithMessage("Invalid toast level.");
    }
}