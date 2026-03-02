using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Behavior.GetBehavior;

public class GetBehaviorEndpoint : EndpointWithoutRequest<GetBehaviorResponse>
{
    public IUserSettingsService SettingsService { get; set; } = default!;

    public override void Configure()
    {
        Get("/settings/behavior");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await SettingsService.GetBehaviorAsync(userId, ct);

        await Send.OkAsync(new GetBehaviorResponse
        {
            SkipDeleteConfirmation = settings.SkipDeleteConfirmation,
            ToastLevel = settings.ToastLevel,
        }, ct);
    }
}
