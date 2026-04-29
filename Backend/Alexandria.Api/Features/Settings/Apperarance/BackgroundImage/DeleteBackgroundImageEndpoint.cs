using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Apperarance.BackgroundImage;

public class DeleteBackgroundImageEndpoint(IUserSettingsService settingsService, IStorageService storageService)
    : EndpointWithoutRequest
{
    public override void Configure() =>
        Delete("/settings/appearance/background-image");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await settingsService.GetAppearanceAsync(userId, ct);

        if (settings.BackgroundImageKey is not null)
        {
            await storageService.DeleteBackgroundImageAsync(settings.BackgroundImageKey, ct);
            settings.BackgroundImageKey = null;
            settings.BackgroundImageUpdatedAt = null;
            await settingsService.SetAppearanceAsync(userId, settings, userId, ct);
        }

        await Send.NoContentAsync(ct);
    }
}