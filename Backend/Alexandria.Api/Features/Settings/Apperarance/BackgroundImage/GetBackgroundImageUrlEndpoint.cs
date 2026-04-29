using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Apperarance.BackgroundImage;

public class BackgroundImageUrlResponse
{
    public string Url { get; set; } = default!;
}

public class GetBackgroundImageUrlEndpoint(IStorageService storageService, IUserSettingsService settingsService)
    : EndpointWithoutRequest<BackgroundImageUrlResponse>
{
    public override void Configure() =>
        Get("/settings/appearance/background-image-url");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await settingsService.GetAppearanceAsync(userId, ct);

        if (settings.BackgroundImageKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var url = await storageService.GenerateBackgroundImageGetUrl(
            settings.BackgroundImageKey,
            TimeSpan.FromMinutes(60));

        await Send.OkAsync(new BackgroundImageUrlResponse { Url = url }, ct);
    }
}