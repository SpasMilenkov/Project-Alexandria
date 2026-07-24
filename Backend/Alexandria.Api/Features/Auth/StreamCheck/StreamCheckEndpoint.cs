using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.StreamCheck;

internal sealed class StreamCheckEndpoint(IFileService fileService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/stream-check");
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

        // uri format: /stream/{fileId}/...
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !Guid.TryParse(segments[1], out var versionId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userId = User.GetUserId();
        var hash = await fileService.VersionBelongsToUserAsync(versionId, userId, ct);

        if (hash is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(cancellation: ct);
    }
}