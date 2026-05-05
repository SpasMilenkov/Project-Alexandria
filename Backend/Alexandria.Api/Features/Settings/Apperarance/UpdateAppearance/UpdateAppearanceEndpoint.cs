using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Apperarance.UpdateAppearance;

public class UpdateAppearanceRequest
{
    public string AccentColor { get; set; } = default!;
    public string BackgroundColor { get; set; } = default!;
    public string? BackgroundImageKey { get; set; }
    public DateTime? BackgroundImageUpdatedAt { get; set; }
    public double BackgroundImageOpacity { get; set; }
    public int GridIconSize { get; set; }
    public int ListIconSize { get; set; }
}

public class UpdateAppearanceEndpoint(IUserSettingsService settingsService)
    : Endpoint<UpdateAppearanceRequest, GetAppearanceResponse>
{
    public override void Configure()
    {
        Put("/settings/appearance");
    }

    public override async Task HandleAsync(UpdateAppearanceRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await settingsService.SetAppearanceAsync(userId, new AppearanceSettingsValue
        {
            AccentColor = req.AccentColor,
            BackgroundColor = req.BackgroundColor,
            BackgroundImageKey = req.BackgroundImageKey,
            BackgroundImageUpdatedAt = req.BackgroundImageUpdatedAt,
            BackgroundImageOpacity = req.BackgroundImageOpacity,
            GridIconSize = req.GridIconSize,
            ListIconSize = req.ListIconSize,
        }, userId, ct);


        var saved = await settingsService.GetAppearanceAsync(userId, ct);
        await Send.OkAsync(GetAppearanceResponse.FromValue(saved), ct);
    }
}