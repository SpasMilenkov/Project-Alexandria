using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage;

public class BackgroundImageUrlResponse
{
    public string Url { get; set; } = default!;
}

public class GetBackgroundImageUrlEndpoint : EndpointWithoutRequest<BackgroundImageUrlResponse>
{
    public IStorageService StorageService { get; set; } = default!;
    public IUserSettingsService SettingsService { get; set; } = default!;

    public override void Configure() =>
        Get("/settings/appearance/background-image-url");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await SettingsService.GetAppearanceAsync(userId, ct);

        if (settings.BackgroundImageKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var url = await StorageService.GenerateBackgroundImageGetUrl(
            settings.BackgroundImageKey,
            TimeSpan.FromMinutes(60));

        await Send.OkAsync(new BackgroundImageUrlResponse { Url = url }, ct);
    }
}
