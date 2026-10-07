using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Home.GetHome;

public sealed class GetHomeEndpoint(IUserSettingsService settingsService)
    : EndpointWithoutRequest<HomeSettingsValue>
{
    public override void Configure()
    {
        Get("/settings/home");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await settingsService.GetHomeAsync(User.GetUserId(), ct), ct);
    }
}