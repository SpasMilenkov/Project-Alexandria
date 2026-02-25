using API.Features.Auth.Extensions;
using API.Features.Settings.Apperarance.API.Features.Settings.Appearance;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.GetAppearance
{
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
}
