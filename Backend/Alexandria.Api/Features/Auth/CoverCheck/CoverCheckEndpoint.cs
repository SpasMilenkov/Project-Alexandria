using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.CoverCheck;

internal sealed class CoverCheckEndpoint(IUnitOfWork unitOfWork) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/cover-check");
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

        // uri format: /covers/{playlistId}, possibly with a ?v= cache-buster
        // appended by the client, which carries no routing meaning here.
        var path = uri.Split('?')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !string.Equals(segments[0], "covers", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(segments[1], out var playlistId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userId = User.GetUserId();
        if (!await unitOfWork.Playlists.IsOwnerAsync(playlistId, userId, ct))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        HttpContext.Response.Headers["X-Content-Path"] = $"covers/{playlistId}";
        await Send.OkAsync(cancellation: ct);
    }
}