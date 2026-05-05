using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Apperarance.GetAppearance;

public class GetAppearanceEndpoint : EndpointWithoutRequest<GetAppearanceResponse>
{
    public IUserSettingsService SettingsService { get; set; } = default!;

    public override void Configure()
    {
        Get("/settings/appearance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await SettingsService.GetAppearanceAsync(userId, ct);

        await Send.OkAsync(GetAppearanceResponse.FromValue(settings), ct);
    }
}