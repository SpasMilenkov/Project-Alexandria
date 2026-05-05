using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Behavior.UpdateBehavior;

public class UpdateBehaviorRequest
{
    public bool SkipDeleteConfirmation { get; set; }
    public ToastLevel ToastLevel { get; set; }
}

public class UpdateBehaviorEndpoint(IUserSettingsService settingsService)
    : Endpoint<UpdateBehaviorRequest, GetBehaviorResponse>
{
    public override void Configure()
    {
        Put("/settings/behavior");
    }

    public override async Task HandleAsync(UpdateBehaviorRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await settingsService.SetBehaviorAsync(userId, new BehaviorSettingsValue
        {
            SkipDeleteConfirmation = req.SkipDeleteConfirmation,
            ToastLevel = req.ToastLevel,
        }, userId, ct);

        var saved = await settingsService.GetBehaviorAsync(userId, ct);

        await Send.OkAsync(new GetBehaviorResponse
        {
            SkipDeleteConfirmation = saved.SkipDeleteConfirmation,
            ToastLevel = saved.ToastLevel,
        }, ct);
    }
}