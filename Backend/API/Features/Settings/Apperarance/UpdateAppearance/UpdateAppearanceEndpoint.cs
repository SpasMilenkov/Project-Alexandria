using API.Features.Auth.Extensions;
using API.Features.Settings.Apperarance.API.Features.Settings.Appearance;
using Common.Services;
using Common.Settings.Values;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.UpdateAppearance;

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

public class UpdateAppearanceEndpoint : Endpoint<UpdateAppearanceRequest, GetAppearanceResponse>
{
    public IUserSettingsService SettingsService { get; set; } = default!;

    public override void Configure()
    {
        Put("/settings/appearance");
    }

    public override async Task HandleAsync(UpdateAppearanceRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await SettingsService.SetAppearanceAsync(userId, new AppearanceSettingsValue
        {
            AccentColor = req.AccentColor,
            BackgroundColor = req.BackgroundColor,
            BackgroundImageKey = req.BackgroundImageKey,
            BackgroundImageUpdatedAt = req.BackgroundImageUpdatedAt,
            BackgroundImageOpacity = req.BackgroundImageOpacity,
            GridIconSize = req.GridIconSize,
            ListIconSize = req.ListIconSize,
        }, userId, ct);



        var saved = await SettingsService.GetAppearanceAsync(userId, ct);
        await Send.OkAsync(GetAppearanceResponse.FromValue(saved), ct);

    }
}
