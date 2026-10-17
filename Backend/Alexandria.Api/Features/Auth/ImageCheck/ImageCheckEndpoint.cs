using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.ImageCheck;

internal sealed class ImageCheckEndpoint(IUserSettingsService settingsService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/image-check");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uri = HttpContext.Request.Headers["X-Original-URI"].FirstOrDefault();
        if (string.IsNullOrEmpty(uri))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // uri format: /images/{userId}, possibly with a ?v= cache-buster
        // appended by the client, which carries no routing meaning here.
        var path = uri.Split('?')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !string.Equals(segments[0], "images", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(segments[1], out var ownerId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userId = User.GetUserId();
        if (ownerId != userId)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var settings = await settingsService.GetAppearanceAsync(userId, ct);
        if (settings.BackgroundImageKey is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        HttpContext.Response.Headers["X-Content-Path"] = settings.BackgroundImageKey;
        await Send.OkAsync(cancellation: ct);
    }
}