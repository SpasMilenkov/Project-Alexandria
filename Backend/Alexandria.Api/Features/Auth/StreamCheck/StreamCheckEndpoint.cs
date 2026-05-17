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
        Console.WriteLine($"uri before if  {uri}");

        if (string.IsNullOrEmpty(uri))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        Console.WriteLine($"uri  {uri}");

        // uri format: /stream/{fileId}/...
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !Guid.TryParse(segments[1], out var fileId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userId = User.GetUserId();
        Console.WriteLine($"User id {userId}");
        var belongs = await fileService.VersionBelongsToUserAsync(fileId, userId, ct);

        if (!belongs)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(cancellation: ct);
    }
}