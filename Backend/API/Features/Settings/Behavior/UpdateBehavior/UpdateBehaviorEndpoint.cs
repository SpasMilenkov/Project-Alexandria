using API.Features.Auth.Extensions;
using Common.Services;
using Common.Settings.Values;
using FastEndpoints;

namespace API.Features.Settings.Behavior.UpdateBehavior;

public class UpdateBehaviorRequest
{
    public bool SkipDeleteConfirmation { get; set; }
    public ToastLevel ToastLevel { get; set; }
}

public class UpdateBehaviorEndpoint : Endpoint<UpdateBehaviorRequest, GetBehaviorResponse>
{
    public IUserSettingsService SettingsService { get; set; } = default!;

    public override void Configure()
    {
        Put("/settings/behavior");
    }

    public override async Task HandleAsync(UpdateBehaviorRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await SettingsService.SetBehaviorAsync(userId, new BehaviorSettingsValue
        {
            SkipDeleteConfirmation = req.SkipDeleteConfirmation,
            ToastLevel = req.ToastLevel,
        }, userId, ct);

        var saved = await SettingsService.GetBehaviorAsync(userId, ct);

        await Send.OkAsync(new GetBehaviorResponse
        {
            SkipDeleteConfirmation = saved.SkipDeleteConfirmation,
            ToastLevel = saved.ToastLevel,
        }, ct);
    }
}
