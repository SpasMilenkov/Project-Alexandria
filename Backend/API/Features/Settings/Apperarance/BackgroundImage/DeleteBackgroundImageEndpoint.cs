using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage
{
    public class DeleteBackgroundImageEndpoint : EndpointWithoutRequest
    {
        public IUserSettingsService SettingsService { get; set; } = default!;
        public IStorageService StorageService { get; set; } = default!;

        public override void Configure() =>
            Delete("/settings/appearance/background-image");

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = User.GetUserId();
            var settings = await SettingsService.GetAppearanceAsync(userId, ct);

            if (settings.BackgroundImageKey is not null)
            {
                await StorageService.DeleteBackgroundImageAsync(settings.BackgroundImageKey, ct);
                settings.BackgroundImageKey = null;
                settings.BackgroundImageUpdatedAt = null;
                await SettingsService.SetAppearanceAsync(userId, settings, userId, ct);
            }

            await Send.NoContentAsync(ct);
        }
    }
}
