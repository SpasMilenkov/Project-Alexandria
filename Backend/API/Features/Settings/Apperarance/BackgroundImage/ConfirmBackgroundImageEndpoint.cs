using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage;

public class ConfirmUploadRequest
{
    public string ObjectKey { get; set; } = default!;
}

public class ConfirmBackgroundImageEndpoint(IUserSettingsService settingsService) : Endpoint<ConfirmUploadRequest, GetAppearanceResponse>
{

    public override void Configure() =>
        Put("/settings/appearance/background-image/confirm");

    public override async Task HandleAsync(ConfirmUploadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await settingsService.GetAppearanceAsync(userId, ct);

        settings.BackgroundImageKey = req.ObjectKey;
        settings.BackgroundImageUpdatedAt = DateTime.UtcNow;

        await settingsService.SetAppearanceAsync(userId, settings, userId, ct);

        var saved = await settingsService.GetAppearanceAsync(userId, ct);
        await Send.OkAsync(GetAppearanceResponse.FromValue(saved), ct);
    }
}
