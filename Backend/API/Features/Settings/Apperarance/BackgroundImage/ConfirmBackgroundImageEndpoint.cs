using API.Features.Auth.Extensions;
using API.Features.Settings.Apperarance.API.Features.Settings.Appearance;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage
{
    public class ConfirmUploadRequest
    {
        public string ObjectKey { get; set; } = default!;
    }

    public class ConfirmBackgroundImageEndpoint : Endpoint<ConfirmUploadRequest, GetAppearanceResponse>
    {
        public IUserSettingsService SettingsService { get; set; } = default!;

        public override void Configure() =>
            Put("/settings/appearance/background-image/confirm");

        public override async Task HandleAsync(ConfirmUploadRequest req, CancellationToken ct)
        {
            var userId = User.GetUserId();
            var settings = await SettingsService.GetAppearanceAsync(userId, ct);

            settings.BackgroundImageKey = req.ObjectKey;
            settings.BackgroundImageUpdatedAt = DateTime.UtcNow;

            await SettingsService.SetAppearanceAsync(userId, settings, userId, ct);

            var saved = await SettingsService.GetAppearanceAsync(userId, ct);
            await Send.OkAsync(GetAppearanceResponse.FromValue(saved), ct);
        }
    }
}   
