using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.PreviewCheck;

internal sealed class PreviewCheckEndpoint(IFileService fileService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/preview-check");
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

        // uri format: /preview/{versionId}/{kind}
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3
            || !Guid.TryParse(segments[1], out var versionId)
            || (segments[2] != "preview" && segments[2] != "thumbnail"))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var kind = segments[2];

        var userId = User.GetUserId();
        var hash = await fileService.VersionBelongsToUserAsync(versionId, userId, ct);
        if (hash is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var folder = kind == "thumbnail" ? "thumbnails" : "previews";
        HttpContext.Response.Headers["X-Content-Path"] = $"{folder}/{hash}";
        await Send.OkAsync(cancellation: ct);
    }
}