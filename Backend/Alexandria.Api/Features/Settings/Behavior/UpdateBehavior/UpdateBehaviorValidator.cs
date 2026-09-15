using Alexandria.Common.Settings.Values;
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

        RuleFor(x => x.AutoPlaylistMinTracks)
            .InclusiveBetween(
                BehaviorSettingsValue.MinAutoPlaylistTracks,
                BehaviorSettingsValue.MaxAutoPlaylistTracks)
            .WithMessage("Auto-playlist minimum must be between 1 and 100 tracks.");
    }
}